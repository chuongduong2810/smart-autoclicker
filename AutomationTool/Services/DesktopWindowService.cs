using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using AutomationTool.Models;

namespace AutomationTool.Services
{
    public class DesktopWindowService : IWindowEnumerationService
    {
        private readonly ILogger<DesktopWindowService> _logger;
        private readonly ConcurrentDictionary<IntPtr, WindowInfo> _windows = new();
        private IntPtr _foregroundWindow;

        public event EventHandler? WindowListUpdated;
        public event EventHandler<WindowInfo>? WindowForegroundChanged;

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

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_APPWINDOW = 0x00040000;
        private const int GWL_STYLE = -16;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_MINIMIZE = 0x20000000;
        private const uint WS_DISABLED = 0x08000000;

        public DesktopWindowService(ILogger<DesktopWindowService> logger)
        {
            _logger = logger;
        }

        public Task<List<WindowInfo>> EnumerateWindowsAsync()
        {
            return Task.Run(() =>
            {
                var windows = new List<WindowInfo>();

                bool Callback(IntPtr hWnd, IntPtr lParam)
                {
                    if (!IsEligibleWindow(hWnd))
                    {
                        return true;
                    }

                    if (GetWindowRect(hWnd, out var rect))
                    {
                        var title = GetWindowTitle(hWnd);
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            GetWindowThreadProcessId(hWnd, out var processId);
                            var processName = TryGetProcessName(processId);

                            windows.Add(new WindowInfo
                            {
                                Handle = hWnd,
                                Title = title,
                                ProcessId = processId,
                                ProcessName = processName,
                                Bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)
                            });
                        }
                    }

                    return true;
                }

                EnumWindows(Callback, IntPtr.Zero);

                return windows;
            });
        }

        public IReadOnlyCollection<WindowInfo> GetCachedWindows()
        {
            return _windows.Values.ToList().AsReadOnly();
        }

        public async Task UpdateWindowCacheAsync()
        {
            var enumeratedWindows = await EnumerateWindowsAsync().ConfigureAwait(false);
            RefreshWindowCache(enumeratedWindows);
            UpdateForegroundWindow();
        }

        public void RefreshWindowCache()
        {
            var enumeratedWindows = EnumerateWindowsAsync().GetAwaiter().GetResult();
            RefreshWindowCache(enumeratedWindows);
        }

        private void RefreshWindowCache(IEnumerable<WindowInfo> enumeratedWindows)
        {
            var handles = new HashSet<IntPtr>();

            foreach (var window in enumeratedWindows)
            {
                handles.Add(window.Handle);
                _windows[window.Handle] = window;
                DispatchWindowUpdateEvent(window);
            }

            foreach (var cachedHandle in _windows.Keys.ToList())
            {
                if (!handles.Contains(cachedHandle))
                {
                    _windows.TryRemove(cachedHandle, out _);
                }
            }

            WindowListUpdated?.Invoke(this, EventArgs.Empty);
        }

        public bool TryGetWindow(IntPtr handle, out WindowInfo windowInfo)
        {
            if (_windows.TryGetValue(handle, out windowInfo!))
            {
                return true;
            }

            if (GetWindowRect(handle, out var rect))
            {
                var title = GetWindowTitle(handle);
                GetWindowThreadProcessId(handle, out var processId);
                var processName = TryGetProcessName(processId);

                windowInfo = new WindowInfo
                {
                    Handle = handle,
                    Title = title,
                    ProcessId = processId,
                    ProcessName = processName,
                    Bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)
                };

                _windows[handle] = windowInfo;
                return true;
            }

            windowInfo = default!;
            return false;
        }

        public bool TryGetWindowBounds(IntPtr handle, out Rectangle bounds)
        {
            if (GetWindowRect(handle, out var rect))
            {
                bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
                return true;
            }

            bounds = Rectangle.Empty;
            return false;
        }

        public bool TryGetClientBounds(IntPtr handle, out Rectangle bounds)
        {
            if (GetClientRect(handle, out var rect))
            {
                bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
                return true;
            }

            bounds = Rectangle.Empty;
            return false;
        }

        private string TryGetProcessName(uint processId)
        {
            try
            {
                if (processId == 0)
                {
                    return "System";
                }

                var process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetWindowTitle(IntPtr handle)
        {
            var length = GetWindowTextLength(handle);
            if (length == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(length + 1);
            GetWindowText(handle, builder, builder.Capacity);
            return builder.ToString();
        }

        private void UpdateForegroundWindow()
        {
            var currentForeground = GetForegroundWindow();

            if (currentForeground != _foregroundWindow)
            {
                _foregroundWindow = currentForeground;

                if (_foregroundWindow != IntPtr.Zero && TryGetWindow(_foregroundWindow, out var info))
                {
                    WindowForegroundChanged?.Invoke(this, info);
                }
            }
        }

        private void DispatchWindowUpdateEvent(WindowInfo window)
        {
            // Placeholder for future event dispatch logic (e.g. message bus)
        }

        private bool IsEligibleWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            var style = GetWindowLong(hWnd, GWL_STYLE);
            if ((style & WS_VISIBLE) == 0)
            {
                return false;
            }

            if ((style & WS_MINIMIZE) != 0)
            {
                return false;
            }

            if ((style & WS_DISABLED) != 0)
            {
                return false;
            }

            if (!IsWindowVisible(hWnd))
            {
                return false;
            }

            var title = GetWindowTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            if (!GetWindowRect(hWnd, out var rect))
            {
                return false;
            }

            if (rect.Right - rect.Left <= 0 || rect.Bottom - rect.Top <= 0)
            {
                return false;
            }

            return true;
        }

        public Task ShowDesktopToolbarAsync() => Task.CompletedTask;

        public Task HideDesktopToolbarAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }
}
