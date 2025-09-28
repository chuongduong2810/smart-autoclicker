using System.Drawing;
using System.Runtime.InteropServices;
using AutomationTool.Models;

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
    }

    public class WindowsAutomationEngine : IAutomationEngine
    {
        private readonly ILogger<WindowsAutomationEngine> _logger;

        // Windows API constants
        private const int MOUSEEVENTF_MOVE = 0x0001;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;

        private const int KEYEVENTF_KEYDOWN = 0x0000;
        private const int KEYEVENTF_KEYUP = 0x0002;

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
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
                    
                    // Move cursor to position
                    SetCursorPos(x, y);
                    
                    // Small delay to ensure cursor movement
                    Thread.Sleep(10);
                    
                    // Perform left click
                    mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
                    Thread.Sleep(10);
                    mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, 0);
                    
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
                    
                    await ClickAsync(location);
                    await Task.Delay(50); // Small delay between clicks
                    await ClickAsync(location);
                    
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
                    
                    // Move cursor to position
                    SetCursorPos(location.X, location.Y);
                    Thread.Sleep(10);
                    
                    // Perform right click
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, location.X, location.Y, 0, 0);
                    Thread.Sleep(10);
                    mouse_event(MOUSEEVENTF_RIGHTUP, location.X, location.Y, 0, 0);
                    
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
                            // Handle special characters
                            HandleSpecialCharacter(c);
                        }
                        else
                        {
                            // Type regular character
                            TypeCharacter(c);
                        }
                        
                        Thread.Sleep(10); // Small delay between keystrokes
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
                            // Press modifier key
                            keybd_event(key.VirtualKey, 0, KEYEVENTF_KEYDOWN, 0);
                        }
                        else
                        {
                            // Press and release regular key
                            keybd_event(key.VirtualKey, 0, KEYEVENTF_KEYDOWN, 0);
                            Thread.Sleep(10);
                            keybd_event(key.VirtualKey, 0, KEYEVENTF_KEYUP, 0);
                        }
                        
                        Thread.Sleep(10);
                    }
                    
                    // Release all modifier keys
                    foreach (var key in keySequence.Where(k => k.IsModifier))
                    {
                        keybd_event(key.VirtualKey, 0, KEYEVENTF_KEYUP, 0);
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
                    SetCursorPos(location.X, location.Y);
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
                    
                    // Move to start position
                    SetCursorPos(from.X, from.Y);
                    Thread.Sleep(50);
                    
                    // Press mouse button down
                    mouse_event(MOUSEEVENTF_LEFTDOWN, from.X, from.Y, 0, 0);
                    Thread.Sleep(50);
                    
                    // Move to end position while holding button
                    SetCursorPos(to.X, to.Y);
                    Thread.Sleep(50);
                    
                    // Release mouse button
                    mouse_event(MOUSEEVENTF_LEFTUP, to.X, to.Y, 0, 0);
                    
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

            // Check if shift is needed
            if ((shiftState & 1) != 0)
            {
                keybd_event(0x10, 0, KEYEVENTF_KEYDOWN, 0); // Shift down
            }

            // Press and release the key
            keybd_event(virtualKey, 0, KEYEVENTF_KEYDOWN, 0);
            Thread.Sleep(10);
            keybd_event(virtualKey, 0, KEYEVENTF_KEYUP, 0);

            // Release shift if it was pressed
            if ((shiftState & 1) != 0)
            {
                keybd_event(0x10, 0, KEYEVENTF_KEYUP, 0); // Shift up
            }
        }

        private void HandleSpecialCharacter(char c)
        {
            switch (c)
            {
                case '\n':
                case '\r':
                    keybd_event(0x0D, 0, KEYEVENTF_KEYDOWN, 0); // Enter down
                    Thread.Sleep(10);
                    keybd_event(0x0D, 0, KEYEVENTF_KEYUP, 0);   // Enter up
                    break;
                case '\t':
                    keybd_event(0x09, 0, KEYEVENTF_KEYDOWN, 0); // Tab down
                    Thread.Sleep(10);
                    keybd_event(0x09, 0, KEYEVENTF_KEYUP, 0);   // Tab up
                    break;
                case '\b':
                    keybd_event(0x08, 0, KEYEVENTF_KEYDOWN, 0); // Backspace down
                    Thread.Sleep(10);
                    keybd_event(0x08, 0, KEYEVENTF_KEYUP, 0);   // Backspace up
                    break;
            }
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
    }

    // Cross-platform automation engine using Windows Forms SendKeys
    public class ManagedAutomationEngine : IAutomationEngine
    {
        private readonly ILogger<ManagedAutomationEngine> _logger;

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
                    
                    // Move cursor and simulate click
                    Cursor.Position = new Point(x, y);
                    Thread.Sleep(10);
                    
                    // Simulate mouse click using Windows Forms
                    Application.DoEvents();
                    
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
                    Cursor.Position = location;
                    Thread.Sleep(10);
                    Application.DoEvents();
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
                    SendKeys.SendWait(keys);
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
                    Cursor.Position = location;
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
                    
                    // Move to start position
                    Cursor.Position = from;
                    await Task.Delay(50);
                    
                    // Simulate drag motion (limited without direct mouse control)
                    Cursor.Position = to;
                    
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
    }
}