using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace AutomationTool.Services
{
    public interface IAutomationEngine
    {
        Task ClickAsync(Point location);
        Task ClickAsync(int x, int y);
        Task DoubleClickAsync(Point location);
        Task RightClickAsync(Point location);
        Task TypeTextAsync(string text);
        Task SendKeysAsync(string keys);
        Task WaitAsync(int milliseconds);
        Task MoveMouseAsync(Point location);
        Task DragAsync(Point from, Point to);
        Point GetMousePosition();
        void SetTargetWindow(IntPtr windowHandle);
        void ClearTargetWindow();
        IntPtr GetTargetWindow();
        bool HasTargetWindow { get; }
    }

    public class WindowsAutomationEngine : IAutomationEngine
    {
        private readonly ILogger<WindowsAutomationEngine> _logger;
        private IntPtr _targetWindowHandle;
        private readonly object _targetWindowLock = new();

        // Windows API constants
        private const int MOUSEEVENTF_MOVE = 0x0001;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;

        private const int KEYEVENTF_KEYDOWN = 0x0000;
        private const int KEYEVENTF_KEYUP = 0x0002;

        private const int MK_LBUTTON = 0x0001;
        private const int MK_RBUTTON = 0x0002;

        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_LBUTTONDBLCLK = 0x0203;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_RBUTTONDBLCLK = 0x0206;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // Virtual key codes
        private static readonly Dictionary<string, byte> VirtualKeys = new Dictionary<string, byte>
        {
            {"ENTER", 0x0D}, {"TAB", 0x09}, {"ESC", 0x1B}, {"SPACE", 0x20},
            {"SHIFT", 0x10}, {"CTRL", 0x11}, {"ALT", 0x12}, {"WIN", 0x5B},
            {"F1", 0x70}, {"F2", 0x71}, {"F3", 0x72}, {"F4", 0x73},
            {"F5", 0x74}, {"F6", 0x75}, {"F7", 0x76}, {"F8", 0x77},
            {"F9", 0x78}, {"F10", 0x79}, {"F11", 0x7A}, {"F12", 0x7B},
            {"LEFT", 0x25}, {"UP", 0x26}, {"RIGHT", 0x27}, {"DOWN", 0x28},
            {"DELETE", 0x2E}, {"HOME", 0x24}, {"END", 0x23},
            {"PAGEUP", 0x21}, {"PAGEDOWN", 0x22}, {"INSERT", 0x2D}
        };

        public WindowsAutomationEngine(ILogger<WindowsAutomationEngine> logger)
        {
            _logger = logger;
        }

        public async Task ClickAsync(Point location)
        {
            await ClickAsync(location.X, location.Y);
        }

        public async Task ClickAsync(int x, int y)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Clicking at position ({X}, {Y})", x, y);

                    if (!TrySendWindowClick(x, y, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP))
                    {
                        SetCursorPos(x, y);
                        Thread.Sleep(10);
                        mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
                        Thread.Sleep(10);
                        mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, 0);
                    }

                    _logger.LogInformation("Clicked at position ({X}, {Y})", x, y);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clicking at position ({X}, {Y})", x, y);
                    throw;
                }
            });
        }

        public async Task DoubleClickAsync(Point location)
        {
            await Task.Run(async () =>
            {
                try
                {
                    _logger.LogDebug("Double-clicking at position ({X}, {Y})", location.X, location.Y);
                    
                    if (!TrySendWindowClick(location.X, location.Y, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, doubleClick: true))
                    {
                        await ClickAsync(location);
                        await Task.Delay(50);
                        await ClickAsync(location);
                    }
                    
                    _logger.LogInformation("Double-clicked at position ({X}, {Y})", location.X, location.Y);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error double-clicking at position ({X}, {Y})", location.X, location.Y);
                    throw;
                }
            });
        }

        public async Task RightClickAsync(Point location)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Right-clicking at position ({X}, {Y})", location.X, location.Y);

                    if (!TrySendWindowClick(location.X, location.Y, MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP))
                    {
                        SetCursorPos(location.X, location.Y);
                        Thread.Sleep(10);
                        mouse_event(MOUSEEVENTF_RIGHTDOWN, location.X, location.Y, 0, 0);
                        Thread.Sleep(10);
                        mouse_event(MOUSEEVENTF_RIGHTUP, location.X, location.Y, 0, 0);
                    }

                    _logger.LogInformation("Right-clicked at position ({X}, {Y})", location.X, location.Y);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error right-clicking at position ({X}, {Y})", location.X, location.Y);
                    throw;
                }
            });
        }

        public async Task TypeTextAsync(string text)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Typing text: {Text}", text);
                    
                    foreach (char c in text)
                    {
                        if (char.IsControl(c))
                        {
                            HandleSpecialCharacter(c);
                        }
                        else
                        {
                            TypeCharacter(c);
                        }
                        
                        Thread.Sleep(10);
                    }
                    
                    _logger.LogInformation("Typed text: {Text}", text);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error typing text: {Text}", text);
                    throw;
                }
            });
        }

        public async Task SendKeysAsync(string keys)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Sending keys: {Keys}", keys);
                    
                    var keySequence = ParseKeySequence(keys);
                    
                    foreach (var key in keySequence)
                    {
                        if (key.IsModifier)
                        {
                            SendKeyToTarget(key.VirtualKey, KEYEVENTF_KEYDOWN);
                        }
                        else
                        {
                            SendKeyToTarget(key.VirtualKey, KEYEVENTF_KEYDOWN);
                            Thread.Sleep(10);
                            SendKeyToTarget(key.VirtualKey, KEYEVENTF_KEYUP);
                        }
                        
                        Thread.Sleep(10);
                    }
                    
                    foreach (var key in keySequence.Where(k => k.IsModifier))
                    {
                        SendKeyToTarget(key.VirtualKey, KEYEVENTF_KEYUP);
                    }
                    
                    _logger.LogInformation("Sent keys: {Keys}", keys);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending keys: {Keys}", keys);
                    throw;
                }
            });
        }

        public async Task WaitAsync(int milliseconds)
        {
            _logger.LogDebug("Waiting for {Milliseconds}ms", milliseconds);
            await Task.Delay(milliseconds);
            _logger.LogInformation("Waited for {Milliseconds}ms", milliseconds);
        }

        public async Task MoveMouseAsync(Point location)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Moving mouse to position ({X}, {Y})", location.X, location.Y);

                    if (!TrySendMouseMove(location.X, location.Y))
                    {
                        SetCursorPos(location.X, location.Y);
                    }

                    _logger.LogInformation("Moved mouse to position ({X}, {Y})", location.X, location.Y);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error moving mouse to position ({X}, {Y})", location.X, location.Y);
                    throw;
                }
            });
        }

        public async Task DragAsync(Point from, Point to)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Dragging from ({FromX}, {FromY}) to ({ToX}, {ToY})", from.X, from.Y, to.X, to.Y);

                    if (!TrySendWindowDrag(from, to))
                    {
                        SetCursorPos(from.X, from.Y);
                        Thread.Sleep(50);
                        mouse_event(MOUSEEVENTF_LEFTDOWN, from.X, from.Y, 0, 0);
                        Thread.Sleep(50);
                        SetCursorPos(to.X, to.Y);
                        Thread.Sleep(50);
                        mouse_event(MOUSEEVENTF_LEFTUP, to.X, to.Y, 0, 0);
                    }

                    _logger.LogInformation("Dragged from ({FromX}, {FromY}) to ({ToX}, {ToY})", from.X, from.Y, to.X, to.Y);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error dragging from ({FromX}, {FromY}) to ({ToX}, {ToY})", from.X, from.Y, to.X, to.Y);
                    throw;
                }
            });
        }

        public Point GetMousePosition()
        {
            GetCursorPos(out POINT point);
            return new Point(point.X, point.Y);
        }

        private void TypeCharacter(char c)
        {
            short vkCode = VkKeyScan(c);
            byte virtualKey = (byte)(vkCode & 0xFF);
            byte shiftState = (byte)(vkCode >> 8);

            void SendShift(bool down)
            {
                SendKeyToTarget(0x10, down ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP);
            }

            bool requiresShift = (shiftState & 1) != 0;

            if (requiresShift)
            {
                SendShift(true);
            }

            SendKeyToTarget(virtualKey, KEYEVENTF_KEYDOWN);
            Thread.Sleep(10);
            SendKeyToTarget(virtualKey, KEYEVENTF_KEYUP);

            if (requiresShift)
            {
                SendShift(false);
            }
        }

        private void HandleSpecialCharacter(char c)
        {
            var key = c switch
            {
                '\n' or '\r' => (byte)0x0D,
                '\t' => (byte)0x09,
                '\b' => (byte)0x08,
                _ => (byte)0
            };

            if (key == 0)
            {
                return;
            }

            SendKeyToTarget(key, KEYEVENTF_KEYDOWN);
            Thread.Sleep(10);
            SendKeyToTarget(key, KEYEVENTF_KEYUP);
        }

        private List<KeyInfo> ParseKeySequence(string keys)
        {
            var keySequence = new List<KeyInfo>();
            var parts = keys.Split('+');

            foreach (var part in parts)
            {
                var trimmedPart = part.Trim().ToUpper();
                
                if (VirtualKeys.ContainsKey(trimmedPart))
                {
                    keySequence.Add(new KeyInfo
                    {
                        VirtualKey = VirtualKeys[trimmedPart],
                        IsModifier = IsModifierKey(trimmedPart)
                    });
                }
                else if (trimmedPart.Length == 1)
                {
                    // Single character
                    short vkCode = VkKeyScan(trimmedPart[0]);
                    keySequence.Add(new KeyInfo
                    {
                        VirtualKey = (byte)(vkCode & 0xFF),
                        IsModifier = false
                    });
                }
            }

            return keySequence;
        }

        private bool IsModifierKey(string key)
        {
            return key == "SHIFT" || key == "CTRL" || key == "ALT" || key == "WIN";
        }

        private class KeyInfo
        {
            public byte VirtualKey { get; set; }
            public bool IsModifier { get; set; }
        }

        private void SendKeyToTarget(byte virtualKey, int keyEvent)
        {
            keybd_event(virtualKey, 0, keyEvent, 0);
        }

        private bool IsModifierVirtualKey(byte virtualKey)
        {
            return virtualKey is 0x10 or 0x11 or 0x12 or 0x5B;
        }

        private int BuildKeyLParam(uint scanCode, bool keyUp)
        {
            var repeatCount = 1;
            var extended = (scanCode & 0x100) != 0;
            var contextCode = 0;
            var previousState = keyUp ? 1 : 0;
            var transitionState = keyUp ? 1 : 0;

            int lParam = repeatCount
                | ((int)scanCode << 16)
                | (extended ? 1 << 24 : 0)
                | (contextCode << 29)
                | (previousState << 30)
                | (transitionState << 31);

            return lParam;
        }

        private Point NormalizeToTargetWindow(int x, int y)
        {
            if (!HasTargetWindow)
            {
                return new Point(x, y);
            }

            if (!IsWindow(_targetWindowHandle))
            {
                ClearTargetWindow();
                return new Point(x, y);
            }

            var point = new POINT { X = x, Y = y };
            if (!ScreenToClient(_targetWindowHandle, ref point))
            {
                return new Point(x, y);
            }

            return new Point(point.X, point.Y);
        }

        private bool TrySendWindowClick(int x, int y, int downFlag, int upFlag, bool doubleClick = false)
        {
            if (!HasTargetWindow)
            {
                return false;
            }

            var normalized = NormalizeToTargetWindow(x, y);
            var lParam = BuildMouseLParam(normalized.X, normalized.Y);

            var downMessage = downFlag switch
            {
                MOUSEEVENTF_LEFTDOWN => WM_LBUTTONDOWN,
                MOUSEEVENTF_RIGHTDOWN => WM_RBUTTONDOWN,
                _ => WM_MOUSEMOVE
            };

            var upMessage = upFlag switch
            {
                MOUSEEVENTF_LEFTUP => WM_LBUTTONUP,
                MOUSEEVENTF_RIGHTUP => WM_RBUTTONUP,
                _ => WM_MOUSEMOVE
            };

            var downWParam = downFlag switch
            {
                MOUSEEVENTF_LEFTDOWN => new IntPtr(MK_LBUTTON),
                MOUSEEVENTF_RIGHTDOWN => new IntPtr(MK_RBUTTON),
                _ => IntPtr.Zero
            };

            PostMessage(_targetWindowHandle, WM_MOUSEMOVE, downWParam, new IntPtr(lParam));

            if (downMessage != WM_MOUSEMOVE)
            {
                PostMessage(_targetWindowHandle, downMessage, downWParam, new IntPtr(lParam));
            }

            if (doubleClick && downMessage == WM_LBUTTONDOWN)
            {
                PostMessage(_targetWindowHandle, WM_LBUTTONUP, IntPtr.Zero, new IntPtr(lParam));
                PostMessage(_targetWindowHandle, WM_LBUTTONDBLCLK, new IntPtr(MK_LBUTTON), new IntPtr(lParam));
                PostMessage(_targetWindowHandle, WM_LBUTTONUP, IntPtr.Zero, new IntPtr(lParam));
                return true;
            }

            if (doubleClick && downMessage == WM_RBUTTONDOWN)
            {
                PostMessage(_targetWindowHandle, WM_RBUTTONUP, IntPtr.Zero, new IntPtr(lParam));
                PostMessage(_targetWindowHandle, WM_RBUTTONDBLCLK, new IntPtr(MK_RBUTTON), new IntPtr(lParam));
                PostMessage(_targetWindowHandle, WM_RBUTTONUP, IntPtr.Zero, new IntPtr(lParam));
                return true;
            }

            if (upMessage != WM_MOUSEMOVE)
            {
                PostMessage(_targetWindowHandle, upMessage, IntPtr.Zero, new IntPtr(lParam));
            }

            return true;
        }

        private bool TrySendMouseMove(int x, int y)
        {
            if (!HasTargetWindow)
            {
                return false;
            }

            var normalized = NormalizeToTargetWindow(x, y);
            var lParam = BuildMouseLParam(normalized.X, normalized.Y);
            PostMessage(_targetWindowHandle, WM_MOUSEMOVE, IntPtr.Zero, new IntPtr(lParam));
            return true;
        }

        private bool TrySendWindowDrag(Point from, Point to)
        {
            if (!HasTargetWindow)
            {
                return false;
            }

            var start = NormalizeToTargetWindow(from.X, from.Y);
            var end = NormalizeToTargetWindow(to.X, to.Y);
            var startLParam = BuildMouseLParam(start.X, start.Y);
            var endLParam = BuildMouseLParam(end.X, end.Y);

            PostMessage(_targetWindowHandle, WM_MOUSEMOVE, IntPtr.Zero, new IntPtr(startLParam));
            PostMessage(_targetWindowHandle, WM_LBUTTONDOWN, new IntPtr(MK_LBUTTON), new IntPtr(startLParam));
            PostMessage(_targetWindowHandle, WM_MOUSEMOVE, new IntPtr(MK_LBUTTON), new IntPtr(endLParam));
            PostMessage(_targetWindowHandle, WM_LBUTTONUP, IntPtr.Zero, new IntPtr(endLParam));

            return true;
        }

        private int BuildMouseLParam(int x, int y)
        {
            return (y << 16) | (x & 0xFFFF);
        }

        public void SetTargetWindow(IntPtr windowHandle)
        {
            lock (_targetWindowLock)
            {
                _targetWindowHandle = windowHandle;
            }
        }

        public void ClearTargetWindow()
        {
            lock (_targetWindowLock)
            {
                _targetWindowHandle = IntPtr.Zero;
            }
        }

        public IntPtr GetTargetWindow()
        {
            lock (_targetWindowLock)
            {
                return _targetWindowHandle;
            }
        }

        public bool HasTargetWindow
        {
            get
            {
                lock (_targetWindowLock)
                {
                    return _targetWindowHandle != IntPtr.Zero;
                }
            }
        }
    }

    // Cross-platform automation engine using Windows Forms SendKeys
    public class ManagedAutomationEngine : IAutomationEngine
    {
        private readonly ILogger<ManagedAutomationEngine> _logger;
        private readonly object _targetWindowLock = new();
        private IntPtr _targetWindowHandle;
        private bool _loggedTargetWarning;

        public ManagedAutomationEngine(ILogger<ManagedAutomationEngine> logger)
        {
            _logger = logger;
        }

        public async Task ClickAsync(Point location)
        {
            await ClickAsync(location.X, location.Y);
        }

        public async Task ClickAsync(int x, int y)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Simulating click at position ({X}, {Y})", x, y);
                    
                    if (!VerifyWindowTargetingSupport())
                    {
                        Cursor.Position = new Point(x, y);
                        Thread.Sleep(10);

                        Application.DoEvents();
                    }
                    
                    _logger.LogInformation("Simulated click at position ({X}, {Y})", x, y);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error simulating click at position ({X}, {Y})", x, y);
                    throw;
                }
            });
        }

        public async Task DoubleClickAsync(Point location)
        {
            await ClickAsync(location);
            await Task.Delay(50);
            await ClickAsync(location);
        }

        public async Task RightClickAsync(Point location)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Simulating right-click at position ({X}, {Y})", location.X, location.Y);
                    if (!VerifyWindowTargetingSupport())
                    {
                        Cursor.Position = location;
                        Thread.Sleep(10);
                        Application.DoEvents();
                    }
                    _logger.LogInformation("Simulated right-click at position ({X}, {Y})", location.X, location.Y);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error simulating right-click at position ({X}, {Y})", location.X, location.Y);
                    throw;
                }
            });
        }

        public async Task TypeTextAsync(string text)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Simulating text input: {Text}", text);
                    SendKeys.SendWait(text);
                    _logger.LogInformation("Simulated text input: {Text}", text);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error simulating text input: {Text}", text);
                    throw;
                }
            });
        }

        public async Task SendKeysAsync(string keys)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Simulating key sequence: {Keys}", keys);
                    if (!VerifyWindowTargetingSupport())
                    {
                        SendKeys.SendWait(keys);
                    }
                    _logger.LogInformation("Simulated key sequence: {Keys}", keys);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error simulating key sequence: {Keys}", keys);
                    throw;
                }
            });
        }

        public async Task WaitAsync(int milliseconds)
        {
            _logger.LogDebug("Waiting for {Milliseconds}ms", milliseconds);
            await Task.Delay(milliseconds);
            _logger.LogInformation("Waited for {Milliseconds}ms", milliseconds);
        }

        public async Task MoveMouseAsync(Point location)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Moving cursor to position ({X}, {Y})", location.X, location.Y);
                    if (!VerifyWindowTargetingSupport())
                    {
                        Cursor.Position = location;
                    }
                    _logger.LogInformation("Moved cursor to position ({X}, {Y})", location.X, location.Y);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error moving cursor to position ({X}, {Y})", location.X, location.Y);
                    throw;
                }
            });
        }

        public async Task DragAsync(Point from, Point to)
        {
            await Task.Run(async () =>
            {
                try
                {
                    _logger.LogDebug("Simulating drag from ({FromX}, {FromY}) to ({ToX}, {ToY})", from.X, from.Y, to.X, to.Y);
                    if (!VerifyWindowTargetingSupport())
                    {
                        Cursor.Position = from;
                        await Task.Delay(50);
                        Cursor.Position = to;
                    }
                    
                    _logger.LogInformation("Simulated drag from ({FromX}, {FromY}) to ({ToX}, {ToY})", from.X, from.Y, to.X, to.Y);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error simulating drag from ({FromX}, {FromY}) to ({ToX}, {ToY})", from.X, from.Y, to.X, to.Y);
                    throw;
                }
            });
        }

        public Point GetMousePosition()
        {
            return Cursor.Position;
        }

        public void SetTargetWindow(IntPtr windowHandle)
        {
            lock (_targetWindowLock)
            {
                _targetWindowHandle = windowHandle;
                _loggedTargetWarning = false;
            }
        }

        public void ClearTargetWindow()
        {
            lock (_targetWindowLock)
            {
                _targetWindowHandle = IntPtr.Zero;
            }
        }

        public IntPtr GetTargetWindow()
        {
            lock (_targetWindowLock)
            {
                return _targetWindowHandle;
            }
        }

        public bool HasTargetWindow
        {
            get
            {
                lock (_targetWindowLock)
                {
                    return _targetWindowHandle != IntPtr.Zero;
                }
            }
        }

        private bool VerifyWindowTargetingSupport()
        {
            if (!HasTargetWindow)
            {
                return false;
            }

            lock (_targetWindowLock)
            {
                if (_targetWindowHandle != IntPtr.Zero && !_loggedTargetWarning)
                {
                    _loggedTargetWarning = true;
                    _logger.LogWarning("Window targeting is not supported by ManagedAutomationEngine. Falling back to Cursor operations.");
                }
            }

            return false;
        }
    }
}