using AutomationTool.Models;

namespace AutomationTool.Services
{
    public interface IDesktopToolbarService : IDisposable
    {
        Task ShowToolbarAsync();
        Task HideToolbarAsync();
        Task UpdateToolbarStateAsync(ScriptExecutionState state, AutomationScript? script);
        event EventHandler<string>? ToolbarActionRequested;
    }
}
