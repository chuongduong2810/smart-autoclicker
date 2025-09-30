using AutomationTool.Models;
using AutomationTool.Utils;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Windows.Forms;

namespace AutomationTool.Services
{
    public class DesktopToolbarService : IDesktopToolbarService, IDisposable
    {
        private DesktopToolbarForm? _toolbarForm;
        private readonly object _lock = new object();
        private Thread? _uiThread;
        private bool _isInitialized = false;
        private readonly ILogger<DesktopToolbarService> _logger;
        private GlobalHotkeyManager? _hotkeyManager;
        
        public event EventHandler<string>? ToolbarActionRequested;

        public DesktopToolbarService(ILogger<DesktopToolbarService> logger)
        {
            _logger = logger;
        }

        public Task ShowToolbarAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("ShowToolbarAsync called");
                    
                    DesktopToolbarForm? form;
                    lock (_lock)
                    {
                        EnsureUIThread();
                        form = _toolbarForm;
                    }
                    
                    if (form != null && !form.IsDisposed)
                    {
                        // Ensure handle is created before invoking
                        if (!form.IsHandleCreated)
                        {
                            _logger.LogDebug("Creating form handle...");
                            var handle = form.Handle; // This forces handle creation
                            _logger.LogDebug("Form handle created: {Handle}", handle);
                        }

                        if (form.InvokeRequired)
                        {
                            form.BeginInvoke(() => // Use BeginInvoke instead of Invoke to avoid blocking
                            {
                                try
                                {
                                    if (!form.Visible)
                                    {
                                        _logger.LogInformation("Showing desktop toolbar at position: ({X}, {Y})", 
                                            form.Location.X, form.Location.Y);
                                        form.Show();
                                        form.BringToFront();
                                        form.TopMost = true;
                                        form.Activate();
                                        _logger.LogInformation("Toolbar shown. Visible={Visible}, IsHandleCreated={IsHandleCreated}, Bounds={Bounds}", 
                                            form.Visible, form.IsHandleCreated, form.Bounds);
                                    }
                                    else
                                    {
                                        _logger.LogDebug("Toolbar already visible");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error in BeginInvoke for showing toolbar");
                                }
                            });
                        }
                        else
                        {
                            if (!form.Visible)
                            {
                                _logger.LogInformation("Showing desktop toolbar (direct call) at position: ({X}, {Y})", 
                                    form.Location.X, form.Location.Y);
                                form.Show();
                                form.BringToFront();
                                form.TopMost = true;
                                form.Activate();
                                _logger.LogInformation("Toolbar shown. Visible={Visible}, IsHandleCreated={IsHandleCreated}, Bounds={Bounds}", 
                                    form.Visible, form.IsHandleCreated, form.Bounds);
                            }
                            else
                            {
                                _logger.LogDebug("Toolbar already visible");
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Toolbar form is null or disposed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing desktop toolbar");
                }
            });
        }

        private void EnsureUIThread()
        {
            if (!_isInitialized)
            {
                _logger.LogInformation("Initializing desktop toolbar UI thread...");
                _uiThread = new Thread(() =>
                {
                    try
                    {
                        _logger.LogDebug("Setting up Windows Forms application context");
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        
                        _logger.LogDebug("Creating desktop toolbar form");
                        _toolbarForm = new DesktopToolbarForm(_logger);
                        _toolbarForm.ActionRequested += OnActionRequested;
                        
                        // Initialize global hotkeys on the UI thread (required for message loop)
                        _logger.LogDebug("Initializing global hotkeys...");
                        _hotkeyManager = new GlobalHotkeyManager();
                        _hotkeyManager.RegisterHotkey(Keys.F9, 
                            () => OnActionRequested(null, "pause_resume"), "F9");
                        _hotkeyManager.RegisterHotkey(Keys.F10, 
                            () => OnActionRequested(null, "stop"), "F10");
                        _hotkeyManager.RegisterHotkey(Keys.F11, 
                            () => OnActionRequested(null, "toggle_toolbar"), "F11");
                        _logger.LogInformation("✓ Global hotkeys registered: F9 (Pause/Resume), F10 (Stop), F11 (Toggle Toolbar)");
                        
                        _isInitialized = true;
                        _logger.LogInformation("✓ Desktop toolbar form created successfully and ready to display");
                        
                        // Keep the UI thread alive - run the message loop
                        // This will block until Application.Exit() is called
                        Application.Run(_toolbarForm);
                        _logger.LogDebug("Desktop toolbar UI thread application loop ended");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in desktop toolbar UI thread");
                    }
                })
                {
                    IsBackground = false,
                    Name = "DesktopToolbarUIThread"
                };
                _uiThread.SetApartmentState(ApartmentState.STA);
                
                _logger.LogDebug("Starting desktop toolbar UI thread");
                _uiThread.Start();
                
                // Wait for initialization with timeout
                var timeout = DateTime.Now.AddSeconds(5);
                while (!_isInitialized && DateTime.Now < timeout)
                {
                    Thread.Sleep(10);
                }
                
                if (_isInitialized)
                {
                    _logger.LogInformation("✓ Desktop toolbar UI thread initialized and running");
                }
                else
                {
                    _logger.LogError("Desktop toolbar UI thread failed to initialize within 5 seconds");
                }
            }
            else
            {
                _logger.LogDebug("Desktop toolbar UI thread already initialized");
            }
        }

        public Task HideToolbarAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("HideToolbarAsync called");
                    
                    DesktopToolbarForm? form;
                    lock (_lock)
                    {
                        form = _toolbarForm;
                    }
                    
                    if (form != null && !form.IsDisposed && form.IsHandleCreated)
                    {
                        if (form.InvokeRequired)
                        {
                            form.BeginInvoke(() =>
                            {
                                try
                                {
                                    form.Hide();
                                    _logger.LogDebug("Desktop toolbar hidden");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error in BeginInvoke for hiding toolbar");
                                }
                            });
                        }
                        else
                        {
                            form.Hide();
                            _logger.LogDebug("Desktop toolbar hidden (direct call)");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Toolbar form is null, disposed, or handle not created - cannot hide");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error hiding desktop toolbar");
                }
            });
        }

        public Task UpdateToolbarStateAsync(ScriptExecutionState state, AutomationScript? script)
        {
            return Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("UpdateToolbarStateAsync called for script: {ScriptName} - {Status}", 
                        script?.Name ?? "Unknown", state.Status);
                    
                    DesktopToolbarForm? form;
                    lock (_lock)
                    {
                        form = _toolbarForm;
                    }
                    
                    if (form != null && !form.IsDisposed)
                    {
                        // Ensure handle is created
                        if (!form.IsHandleCreated)
                        {
                            _logger.LogDebug("Creating form handle for update...");
                            var handle = form.Handle;
                        }

                        if (form.InvokeRequired)
                        {
                            form.BeginInvoke(() =>
                            {
                                try
                                {
                                    form.UpdateState(state, script);
                                    _logger.LogDebug("Toolbar state updated successfully");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error in BeginInvoke for updating toolbar state");
                                }
                            });
                        }
                        else
                        {
                            form.UpdateState(state, script);
                            _logger.LogDebug("Toolbar state updated successfully (direct call)");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Cannot update toolbar state - form is null or disposed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating toolbar state");
                }
            });
        }

        private void OnActionRequested(object? sender, string action)
        {
            _logger.LogDebug("Hotkey action requested: {Action}", action);
            
            // Handle toggle_toolbar action locally
            if (action == "toggle_toolbar")
            {
                lock (_lock)
                {
                    if (_toolbarForm != null && !_toolbarForm.IsDisposed && _toolbarForm.IsHandleCreated)
                    {
                        _toolbarForm.BeginInvoke(() =>
                        {
                            try
                            {
                                if (_toolbarForm.Visible)
                                {
                                    _logger.LogInformation("F11 pressed - hiding toolbar");
                                    _toolbarForm.Hide();
                                }
                                else
                                {
                                    _logger.LogInformation("F11 pressed - showing toolbar");
                                    _toolbarForm.Show();
                                    _toolbarForm.BringToFront();
                                    _toolbarForm.Activate();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error toggling toolbar visibility");
                            }
                        });
                    }
                }
            }
            else
            {
                // Forward other actions to subscribers
                ToolbarActionRequested?.Invoke(this, action);
            }
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing DesktopToolbarService...");
            
            try
            {
                // Dispose hotkey manager first
                if (_hotkeyManager != null)
                {
                    _logger.LogDebug("Disposing global hotkey manager...");
                    _hotkeyManager.Dispose();
                    _hotkeyManager = null;
                }
                
                lock (_lock)
                {
                    if (_toolbarForm != null && !_toolbarForm.IsDisposed)
                    {
                        _toolbarForm.ActionRequested -= OnActionRequested;
                        
                        if (_toolbarForm.IsHandleCreated)
                        {
                            try
                            {
                                _toolbarForm.Invoke(() =>
                                {
                                    _logger.LogDebug("Closing toolbar form...");
                                    _toolbarForm.Close();
                                    _toolbarForm.Dispose();
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error closing toolbar form via Invoke");
                            }
                        }
                    }
                    
                    // The form closing will end Application.Run(_toolbarForm) automatically
                    // Just wait for the thread to finish
                    if (_uiThread != null && _uiThread.IsAlive)
                    {
                        _logger.LogDebug("Waiting for UI thread to exit...");
                        
                        // Give it time to exit gracefully (form is already closing above)
                        if (!_uiThread.Join(3000))
                        {
                            _logger.LogWarning("UI thread did not exit gracefully within 3 seconds");
                            // Don't force abort - let it finish naturally
                        }
                        else
                        {
                            _logger.LogInformation("UI thread exited successfully");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during DesktopToolbarService disposal");
            }
            
            _logger.LogInformation("DesktopToolbarService disposed");
        }
    }

    internal class DesktopToolbarForm : Form
    {
        private readonly Label _scriptNameLabel;
        private readonly Label _statusLabel;
        private readonly Label _progressLabel;
        private readonly Button _pauseResumeButton;
        private readonly Button _stopButton;
        private readonly Button _hideButton;
        private readonly ProgressBar _progressBar;
        private readonly Panel _progressPanel;
        
        private ScriptExecutionState? _currentState;
        private AutomationScript? _currentScript;

        public event EventHandler<string>? ActionRequested;

        private readonly ILogger? _formLogger;

        public DesktopToolbarForm(ILogger? logger = null)
        {
            _formLogger = logger;
            InitializeComponent();
            SetupForm();
            
            // Add event handlers for debugging
            this.Load += (s, e) => _formLogger?.LogInformation("✓ Toolbar form Load event fired");
            this.Shown += (s, e) => _formLogger?.LogInformation("✓ Toolbar form Shown event fired - form is now visible on screen");
            this.VisibleChanged += (s, e) => _formLogger?.LogDebug("Toolbar form VisibleChanged: {Visible}", Visible);
            this.FormClosing += (s, e) =>
            {
                _formLogger?.LogInformation("Toolbar form is closing. Reason: {CloseReason}", e.CloseReason);
                // Don't cancel the close
                e.Cancel = false;
            };
            this.FormClosed += (s, e) => _formLogger?.LogInformation("✓ Toolbar form closed successfully");
            
            // Add subtle border effect
            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(40, 40, 50), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            };
            
            // Create controls with modern styling
            _scriptNameLabel = new Label
            {
                Text = "No Active Script",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 230, 235),
                AutoSize = true,
                Location = new Point(15, 10)
            };

            _statusLabel = new Label
            {
                Text = "IDLE",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(150, 150, 160),
                AutoSize = true,
                Location = new Point(15, 30)
            };

            _pauseResumeButton = new Button
            {
                Text = "⏸",
                Font = new Font("Segoe UI", 14),
                Size = new Size(42, 40),
                Location = new Point(210, 8),
                BackColor = Color.FromArgb(55, 55, 65),
                ForeColor = Color.FromArgb(200, 200, 210),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            _pauseResumeButton.FlatAppearance.BorderSize = 0;
            _pauseResumeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 85);
            _pauseResumeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 40, 50);
            _pauseResumeButton.Click += (s, e) => ActionRequested?.Invoke(this, "pause_resume");

            _stopButton = new Button
            {
                Text = "⏹",
                Font = new Font("Segoe UI", 14),
                Size = new Size(42, 40),
                Location = new Point(258, 8),
                BackColor = Color.FromArgb(85, 30, 35),
                ForeColor = Color.FromArgb(255, 120, 130),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            _stopButton.FlatAppearance.BorderSize = 0;
            _stopButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(110, 40, 45);
            _stopButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(60, 20, 25);
            _stopButton.Click += (s, e) => ActionRequested?.Invoke(this, "stop");

            _hideButton = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 11),
                Size = new Size(30, 40),
                Location = new Point(305, 8),
                BackColor = Color.FromArgb(35, 35, 40),
                ForeColor = Color.FromArgb(150, 150, 160),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            _hideButton.FlatAppearance.BorderSize = 0;
            _hideButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 60);
            _hideButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(25, 25, 30);
            _hideButton.Click += (s, e) => Hide();

            _progressPanel = new Panel
            {
                Location = new Point(15, 52),
                Size = new Size(320, 22),
                Visible = false,
                BackColor = Color.Transparent
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(0, 0),
                Size = new Size(240, 18),
                Style = ProgressBarStyle.Continuous,
                ForeColor = Color.FromArgb(70, 180, 130)
            };

            _progressLabel = new Label
            {
                Text = "0/0",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 220, 180),
                AutoSize = true,
                Location = new Point(245, 2)
            };

            _progressPanel.Controls.Add(_progressBar);
            _progressPanel.Controls.Add(_progressLabel);

            // Add a subtle accent line at the top for visual polish
            var accentLine = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(340, 2),
                BackColor = Color.FromArgb(70, 180, 130)
            };

            Controls.AddRange(new Control[] {
                accentLine,
                _scriptNameLabel, _statusLabel, _pauseResumeButton, 
                _stopButton, _hideButton, _progressPanel
            });

            // Initially hide the form
            WindowState = FormWindowState.Normal;
            Visible = false;
        }

        private void SetupForm()
        {
            Text = "Automation Toolbar";
            Size = new Size(340, 58);
            FormBorderStyle = FormBorderStyle.None; // Borderless for modern look
            BackColor = Color.FromArgb(20, 20, 25); // Darker, more modern background
            TopMost = true;
            ShowInTaskbar = false; // Hide from taskbar
            StartPosition = FormStartPosition.Manual;
            Opacity = 0.96; // Slight transparency for modern effect
            
            // Position at top-right of screen
            var screen = Screen.PrimaryScreen?.WorkingArea ?? Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            var x = screen.Right - Width - 10;
            var y = 10;
            Location = new Point(x, y);
            
            _formLogger?.LogInformation("Toolbar form positioned at: ({X}, {Y}), Size: {Width}x{Height}", x, y, Width, Height);
            _formLogger?.LogInformation("Screen working area: {WorkingArea}", screen);

            // Enable rounded corners for modern look
            try
            {
                Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 12, 12));
            }
            catch (Exception ex)
            {
                _formLogger?.LogWarning(ex, "Could not create rounded corners, using square form");
            }
            
            // Add shadow effect using a panel
            var shadowPanel = new Panel
            {
                Location = new Point(2, 2),
                Size = new Size(Width - 4, Height - 4),
                BackColor = Color.Transparent
            };
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            ResumeLayout(false);
        }

        public void UpdateState(ScriptExecutionState state, AutomationScript? script)
        {
            if (InvokeRequired)
            {
                Invoke(() => UpdateState(state, script));
                return;
            }

            _formLogger?.LogDebug("Updating toolbar state: {ScriptName} - {Status}", script?.Name ?? "Unknown", state.Status);

            _currentState = state;
            _currentScript = script;

            // Update script name
            _scriptNameLabel.Text = script?.Name ?? "Unknown Script";

            // Update status
            var duration = DateTime.Now - state.StartTime;
            _statusLabel.Text = $"{state.Status?.ToUpper()} • {duration:hh\\:mm\\:ss}";

            // Update pause/resume button with modern colors
            if (state.Status == "Running")
            {
                _pauseResumeButton.Text = "⏸";
                _pauseResumeButton.BackColor = Color.FromArgb(100, 80, 30);
                _pauseResumeButton.ForeColor = Color.FromArgb(255, 200, 100);
                _statusLabel.ForeColor = Color.FromArgb(100, 255, 150);
            }
            else if (state.Status == "Paused")
            {
                _pauseResumeButton.Text = "▶";
                _pauseResumeButton.BackColor = Color.FromArgb(40, 100, 60);
                _pauseResumeButton.ForeColor = Color.FromArgb(120, 255, 170);
                _statusLabel.ForeColor = Color.FromArgb(255, 200, 100);
            }
            else
            {
                _pauseResumeButton.Text = "⏸";
                _pauseResumeButton.BackColor = Color.FromArgb(55, 55, 65);
                _pauseResumeButton.ForeColor = Color.FromArgb(200, 200, 210);
                _statusLabel.ForeColor = Color.FromArgb(150, 150, 160);
            }

            // Update progress with modern styling
            if (state.IsInfiniteRepeat)
            {
                _progressPanel.Visible = true;
                _progressBar.Style = ProgressBarStyle.Marquee;
                _progressBar.MarqueeAnimationSpeed = 30;
                _progressLabel.Text = $"∞ Loop #{state.CurrentRepeat}";
                Height = 80;
            }
            else if (state.TotalRepeats > 1)
            {
                _progressPanel.Visible = true;
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Maximum = state.TotalRepeats;
                _progressBar.Value = Math.Min(state.CurrentRepeat, state.TotalRepeats);
                _progressLabel.Text = $"{state.CurrentRepeat} / {state.TotalRepeats}";
                Height = 80;
            }
            else
            {
                _progressPanel.Visible = false;
                Height = 58;
            }

            if (!Visible)
            {
                _formLogger?.LogInformation("✓ Desktop toolbar is now being shown on screen");
                Show();
                BringToFront();
            }
            else
            {
                _formLogger?.LogDebug("Desktop toolbar is already visible");
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            _formLogger?.LogDebug("SetVisibleCore called with value={Value}", value);
            base.SetVisibleCore(value);
            if (value)
            {
                _formLogger?.LogInformation("Form SetVisibleCore making visible - bringing to front and setting TopMost");
                BringToFront();
                TopMost = true;
            }
        }

        protected override bool ShowWithoutActivation => false; // Changed to false to ensure activation

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
    }
}
