using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AutomationTool.Models;
using AutomationTool.Utils;

namespace AutomationTool.Services
{
    public interface IScreenshotService
    {
        Task<byte[]> CaptureFullScreenAsync();
        Task<byte[]> CaptureRegionAsync(ScreenRegion region);
        Task<byte[]> CaptureRegionAsync(int x, int y, int width, int height);
        Task<string> SaveScreenshotAsync(byte[] imageData, string fileName = "");
        Task<TemplateImage> CreateTemplateImageAsync(ScreenRegion region, string name);
        Rectangle GetScreenBounds();
        List<Rectangle> GetAllScreenBounds();
        Task<Rectangle?> SelectDesktopRegionAsync();
    }

    public class ScreenshotService : IScreenshotService
    {
        private readonly string _screenshotsPath;
        private readonly ILogger<ScreenshotService> _logger;

        // Windows API imports for screen capture
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const uint SRCCOPY = 0x00CC0020;

        public ScreenshotService(ILogger<ScreenshotService> logger)
        {
            _logger = logger;
            _screenshotsPath = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            
            // Ensure screenshots directory exists
            if (!Directory.Exists(_screenshotsPath))
            {
                Directory.CreateDirectory(_screenshotsPath);
                _logger.LogInformation("Created screenshots directory: {Path}", _screenshotsPath);
            }
        }

        public async Task<byte[]> CaptureFullScreenAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var bounds = GetScreenBounds();
                    return CaptureRegion(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error capturing full screen");
                    throw;
                }
            });
        }

        public async Task<byte[]> CaptureRegionAsync(ScreenRegion region)
        {
            return await CaptureRegionAsync(region.X, region.Y, region.Width, region.Height);
        }

        public async Task<byte[]> CaptureRegionAsync(int x, int y, int width, int height)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return CaptureRegion(x, y, width, height);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error capturing screen region at ({X}, {Y}) with size {Width}x{Height}", x, y, width, height);
                    throw;
                }
            });
        }

        private byte[] CaptureRegion(int x, int y, int width, int height)
        {
            IntPtr desktopDC = IntPtr.Zero;
            IntPtr memoryDC = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                // Get desktop device context
                desktopDC = GetDC(GetDesktopWindow());
                if (desktopDC == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to get desktop device context");

                // Create memory device context
                memoryDC = CreateCompatibleDC(desktopDC);
                if (memoryDC == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to create compatible device context");

                // Create bitmap
                bitmap = CreateCompatibleBitmap(desktopDC, width, height);
                if (bitmap == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to create compatible bitmap");

                // Select bitmap into memory DC
                oldBitmap = SelectObject(memoryDC, bitmap);

                // Copy screen content to bitmap
                if (!BitBlt(memoryDC, 0, 0, width, height, desktopDC, x, y, SRCCOPY))
                    throw new InvalidOperationException("Failed to capture screen content");

                // Convert to managed bitmap
                using var managedBitmap = Image.FromHbitmap(bitmap);
                using var ms = new MemoryStream();
                managedBitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            finally
            {
                // Cleanup
                if (oldBitmap != IntPtr.Zero) SelectObject(memoryDC, oldBitmap);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (memoryDC != IntPtr.Zero) DeleteDC(memoryDC);
                if (desktopDC != IntPtr.Zero) ReleaseDC(GetDesktopWindow(), desktopDC);
            }
        }

        public async Task<string> SaveScreenshotAsync(byte[] imageData, string fileName = "")
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            }

            if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".png";
            }

            var filePath = Path.Combine(_screenshotsPath, fileName);
            
            await File.WriteAllBytesAsync(filePath, imageData);
            _logger.LogInformation("Screenshot saved to: {FilePath}", filePath);
            
            return filePath;
        }

        public async Task<TemplateImage> CreateTemplateImageAsync(ScreenRegion region, string name)
        {
            var imageData = await CaptureRegionAsync(region);
            var fileName = $"template_{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filePath = await SaveScreenshotAsync(imageData, fileName);

            return new TemplateImage
            {
                Name = name,
                FilePath = filePath,
                ImageData = imageData,
                CaptureRegion = region.ToRectangle(),
                CreatedAt = DateTime.Now
            };
        }

        public Rectangle GetScreenBounds()
        {
            var width = GetSystemMetrics(SM_CXSCREEN);
            var height = GetSystemMetrics(SM_CYSCREEN);
            return new Rectangle(0, 0, width, height);
        }

        public List<Rectangle> GetAllScreenBounds()
        {
            // For now, return single primary screen
            // TODO: Implement multi-monitor support
            var bounds = new List<Rectangle> { GetScreenBounds() };
            return bounds;
        }

        public async Task<Rectangle?> SelectDesktopRegionAsync()
        {
            try
            {
                _logger.LogInformation("Starting desktop region selection...");
                var selectedRegion = await DesktopRegionSelector.SelectRegionAsync();
                
                if (selectedRegion.HasValue)
                {
                    _logger.LogInformation("Desktop region selected: {X}, {Y}, {Width}x{Height}", 
                        selectedRegion.Value.X, selectedRegion.Value.Y, selectedRegion.Value.Width, selectedRegion.Value.Height);
                }
                else
                {
                    _logger.LogInformation("Desktop region selection cancelled");
                }
                
                return selectedRegion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during desktop region selection");
                throw;
            }
        }
    }

    // Alternative implementation using System.Drawing.Graphics for environments where P/Invoke is restricted
    public class ManagedScreenshotService : IScreenshotService
    {
        private readonly string _screenshotsPath;
        private readonly ILogger<ManagedScreenshotService> _logger;

        public ManagedScreenshotService(ILogger<ManagedScreenshotService> logger)
        {
            _logger = logger;
            _screenshotsPath = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            
            if (!Directory.Exists(_screenshotsPath))
            {
                Directory.CreateDirectory(_screenshotsPath);
            }
        }

        public async Task<byte[]> CaptureFullScreenAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var bounds = GetScreenBounds();
                    using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                    using var graphics = Graphics.FromImage(bitmap);
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    
                    using var ms = new MemoryStream();
                    bitmap.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error capturing full screen with managed API");
                    throw;
                }
            });
        }

        public async Task<byte[]> CaptureRegionAsync(ScreenRegion region)
        {
            return await CaptureRegionAsync(region.X, region.Y, region.Width, region.Height);
        }

        public async Task<byte[]> CaptureRegionAsync(int x, int y, int width, int height)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var bitmap = new Bitmap(width, height);
                    using var graphics = Graphics.FromImage(bitmap);
                    graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
                    
                    using var ms = new MemoryStream();
                    bitmap.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error capturing region with managed API");
                    throw;
                }
            });
        }

        public async Task<string> SaveScreenshotAsync(byte[] imageData, string fileName = "")
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            }

            if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".png";
            }

            var filePath = Path.Combine(_screenshotsPath, fileName);
            await File.WriteAllBytesAsync(filePath, imageData);
            
            return filePath;
        }

        public async Task<TemplateImage> CreateTemplateImageAsync(ScreenRegion region, string name)
        {
            var imageData = await CaptureRegionAsync(region);
            var fileName = $"template_{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filePath = await SaveScreenshotAsync(imageData, fileName);

            return new TemplateImage
            {
                Name = name,
                FilePath = filePath,
                ImageData = imageData,
                CaptureRegion = region.ToRectangle(),
                CreatedAt = DateTime.Now
            };
        }

        public Rectangle GetScreenBounds()
        {
            return Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        }

        public List<Rectangle> GetAllScreenBounds()
        {
            return Screen.AllScreens.Select(s => s.Bounds).ToList();
        }

        public async Task<Rectangle?> SelectDesktopRegionAsync()
        {
            // Use the same desktop region selector
            return await DesktopRegionSelector.SelectRegionAsync();
        }
    }
}