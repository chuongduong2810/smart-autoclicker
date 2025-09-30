using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AutomationTool.Services
{
    public class DesktopWindowService : IDisposable
    {
        private readonly ILogger<DesktopWindowService> _logger;
        private Process? _toolbarProcess;
        private readonly string _baseUrl;

        // Windows API declarations
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_APPWINDOW = 0x00040000;

        public DesktopWindowService(ILogger<DesktopWindowService> logger)
        {
            _logger = logger;
            _baseUrl = "http://localhost:5219";
        }

        public async Task ShowDesktopToolbarAsync()
        {
            try
            {
                if (_toolbarProcess == null || _toolbarProcess.HasExited)
                {
                    _logger.LogInformation("Starting desktop toolbar browser window");

                    // Create a minimal browser window for the toolbar
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "msedge.exe", // Try Edge first
                        Arguments = $"--app={_baseUrl}/toolbar-only --window-size=400,100 --window-position=1520,20 --disable-web-security --disable-features=VizDisplayCompositor",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    };

                    try
                    {
                        _toolbarProcess = Process.Start(startInfo);
                    }
                    catch
                    {
                        // Fallback to Chrome
                        startInfo.FileName = "chrome.exe";
                        try
                        {
                            _toolbarProcess = Process.Start(startInfo);
                        }
                        catch
                        {
                            // Final fallback to default browser
                            startInfo.FileName = _baseUrl + "/toolbar-only";
                            startInfo.Arguments = "";
                            startInfo.UseShellExecute = true;
                            _toolbarProcess = Process.Start(startInfo);
                        }
                    }

                    if (_toolbarProcess != null)
                    {
                        // Wait a moment for the window to appear
                        await Task.Delay(2000);

                        // Try to modify the window to make it always on top
                        MakeWindowAlwaysOnTop();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting desktop toolbar window");
            }
        }

        private void MakeWindowAlwaysOnTop()
        {
            try
            {
                // This is a best-effort attempt to find and modify the browser window
                // Note: This approach has limitations due to security restrictions
                if (_toolbarProcess != null && !_toolbarProcess.HasExited)
                {
                    var mainWindowHandle = _toolbarProcess.MainWindowHandle;
                    if (mainWindowHandle != IntPtr.Zero)
                    {
                        // Make window always on top
                        SetWindowPos(mainWindowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                        
                        // Try to remove it from taskbar
                        var exStyle = GetWindowLong(mainWindowHandle, GWL_EXSTYLE);
                        SetWindowLong(mainWindowHandle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not modify window properties: {Error}", ex.Message);
            }
        }

        public Task HideDesktopToolbarAsync()
        {
            try
            {
                if (_toolbarProcess != null && !_toolbarProcess.HasExited)
                {
                    _toolbarProcess.CloseMainWindow();
                    _toolbarProcess.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding desktop toolbar window");
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
                if (_toolbarProcess != null && !_toolbarProcess.HasExited)
                {
                    _toolbarProcess.Kill();
                    _toolbarProcess.Dispose();
                }
            }
            catch
            {
                // Ignore disposal errors
            }
        }
    }
}
