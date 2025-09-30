using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutomationTool.Utils
{
    public static class DesktopRegionSelector
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, 
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int LWA_ALPHA = 0x2;

        public static Task<Rectangle?> SelectRegionAsync()
        {
            var tcs = new TaskCompletionSource<Rectangle?>();
            
            // Run on a separate thread to avoid blocking the main UI
            var thread = new Thread(() =>
            {
                try
                {
                    var result = ShowRegionSelector();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            
            return tcs.Task;
        }

        private static Rectangle? ShowRegionSelector()
        {
            Rectangle? selectedRegion = null;
            
            // Create a full-screen overlay form
            using (var overlay = new FullScreenOverlay())
            {
                overlay.RegionSelected += (sender, region) =>
                {
                    selectedRegion = region;
                };
                
                Application.Run(overlay);
            }
            
            return selectedRegion;
        }
    }

    public class FullScreenOverlay : Form
    {
        public event EventHandler<Rectangle> RegionSelected;
        
        private bool _isSelecting = false;
        private Point _startPoint;
        private Point _endPoint;
        private Rectangle _selectionRect;
        
        public FullScreenOverlay()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            // Set up full-screen overlay
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            this.Opacity = 0.3;
            this.Cursor = Cursors.Cross;
            
            // Make the form cover all screens
            var bounds = SystemInformation.VirtualScreen;
            this.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            
            // Set up event handlers
            this.MouseDown += OnMouseDown;
            this.MouseMove += OnMouseMove;
            this.MouseUp += OnMouseUp;
            this.KeyDown += OnKeyDown;
            this.Paint += OnPaint;
            
            // Enable double buffering
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | 
                         ControlStyles.UserPaint | 
                         ControlStyles.DoubleBuffer, true);
        }
        
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isSelecting = true;
                _startPoint = e.Location;
                _endPoint = e.Location;
                this.Capture = true;
            }
        }
        
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isSelecting)
            {
                _endPoint = e.Location;
                UpdateSelectionRect();
                this.Invalidate();
            }
        }
        
        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (_isSelecting && e.Button == MouseButtons.Left)
            {
                _isSelecting = false;
                this.Capture = false;
                
                UpdateSelectionRect();
                
                if (_selectionRect.Width > 5 && _selectionRect.Height > 5)
                {
                    RegionSelected?.Invoke(this, _selectionRect);
                }
                
                this.Close();
            }
        }
        
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }
        
        private void OnPaint(object sender, PaintEventArgs e)
        {
            if (_isSelecting && _selectionRect.Width > 0 && _selectionRect.Height > 0)
            {
                // Draw selection rectangle
                using (var pen = new Pen(Color.Red, 2))
                {
                    e.Graphics.DrawRectangle(pen, _selectionRect);
                }
                
                // Draw semi-transparent fill
                using (var brush = new SolidBrush(Color.FromArgb(50, Color.Blue)))
                {
                    e.Graphics.FillRectangle(brush, _selectionRect);
                }
                
                // Draw coordinates
                var text = $"({_selectionRect.X}, {_selectionRect.Y}) {_selectionRect.Width}x{_selectionRect.Height}";
                using (var font = new Font("Arial", 12))
                using (var brush = new SolidBrush(Color.White))
                using (var backgroundBrush = new SolidBrush(Color.FromArgb(150, Color.Black)))
                {
                    var textSize = e.Graphics.MeasureString(text, font);
                    var textRect = new RectangleF(_selectionRect.X, _selectionRect.Y - 25, textSize.Width + 10, textSize.Height + 5);
                    e.Graphics.FillRectangle(backgroundBrush, textRect);
                    e.Graphics.DrawString(text, font, brush, textRect.X + 5, textRect.Y + 2);
                }
            }
            
            // Draw instructions
            var instructions = "Click and drag to select a region. Press ESC to cancel.";
            using (var font = new Font("Arial", 14, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            using (var backgroundBrush = new SolidBrush(Color.FromArgb(150, Color.Black)))
            {
                var textSize = e.Graphics.MeasureString(instructions, font);
                var textRect = new RectangleF((this.Width - textSize.Width) / 2, 20, textSize.Width + 20, textSize.Height + 10);
                e.Graphics.FillRectangle(backgroundBrush, textRect);
                e.Graphics.DrawString(instructions, font, brush, textRect.X + 10, textRect.Y + 5);
            }
        }
        
        private void UpdateSelectionRect()
        {
            var x = Math.Min(_startPoint.X, _endPoint.X);
            var y = Math.Min(_startPoint.Y, _endPoint.Y);
            var width = Math.Abs(_endPoint.X - _startPoint.X);
            var height = Math.Abs(_endPoint.Y - _startPoint.Y);
            
            _selectionRect = new Rectangle(x, y, width, height);
        }
        
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
