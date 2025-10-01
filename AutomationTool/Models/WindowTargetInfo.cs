using System;
using System.Drawing;

namespace AutomationTool.Models
{
    public class WindowInfo
    {
        public IntPtr Handle { get; init; }
        public string Title { get; init; } = string.Empty;
        public string ProcessName { get; init; } = string.Empty;
        public uint ProcessId { get; init; }
        public Rectangle Bounds { get; init; }

        public long HandleValue => Handle.ToInt64();
        public string HandleHex => $"0x{Handle.ToInt64():X}";
        public string DisplayName => $"{Title} ({ProcessName})";

        public override string ToString()
        {
            return $"{Title} ({ProcessName}) [{HandleHex}]";
        }
    }
}

