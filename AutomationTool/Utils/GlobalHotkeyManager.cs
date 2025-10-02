using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutomationTool.Utils
{
    public class GlobalHotkeyManager : IDisposable
    {
        public const uint MOD_NONE = 0x0000;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        private const int WM_HOTKEY = 0x0312;
        
        private readonly Dictionary<int, Action> _hotkeyActions = new();
        private readonly NativeWindow _window;
        private int _currentId = 9000;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public GlobalHotkeyManager()
        {
            _window = new HotkeyWindow();
            ((HotkeyWindow)_window).HotkeyPressed += OnHotkeyPressed;
        }

        public bool RegisterHotkey(Keys key, Action action, string keyName)
        {
            return RegisterHotkey(key, MOD_NONE, action, keyName);
        }

        public bool RegisterHotkey(Keys key, uint modifiers, Action action, string keyName)
        {
            var id = ++_currentId;
            if (RegisterHotKey(_window.Handle, id, (int)modifiers, (int)key))
            {
                _hotkeyActions[id] = action;
                return true;
            }
            return false;
        }

        private void OnHotkeyPressed(object? sender, int id)
        {
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action.Invoke();
            }
        }

        public void Dispose()
        {
            foreach (var id in _hotkeyActions.Keys)
            {
                UnregisterHotKey(_window.Handle, id);
            }
            _hotkeyActions.Clear();
            _window.ReleaseHandle();
            _window.DestroyHandle();
        }

        private class HotkeyWindow : NativeWindow
        {
            public event EventHandler<int>? HotkeyPressed;

            public HotkeyWindow()
            {
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    var id = m.WParam.ToInt32();
                    HotkeyPressed?.Invoke(this, id);
                }
                base.WndProc(ref m);
            }
        }
    }
}
