using System.Drawing;
using AutomationTool.Models;

namespace AutomationTool.Services
{
    public interface IWindowEnumerationService
    {
        event EventHandler? WindowListUpdated;
        event EventHandler<WindowInfo>? WindowForegroundChanged;

        Task<List<WindowInfo>> EnumerateWindowsAsync();
        IReadOnlyCollection<WindowInfo> GetCachedWindows();
        Task UpdateWindowCacheAsync();
        void RefreshWindowCache();
        bool TryGetWindow(IntPtr handle, out WindowInfo windowInfo);
        bool TryGetWindowBounds(IntPtr handle, out Rectangle bounds);
        bool TryGetClientBounds(IntPtr handle, out Rectangle bounds);
    }
}

