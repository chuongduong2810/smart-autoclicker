using AutomationTool.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutomationTool.Services
{
    public class DesktopToolbarManagerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DesktopToolbarManagerService> _logger;
        private IScriptExecutionService? _scriptExecution;
        private IScriptStorageService? _scriptStorage;
        private IDesktopToolbarService? _toolbarService;

        public DesktopToolbarManagerService(IServiceProvider serviceProvider, ILogger<DesktopToolbarManagerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DesktopToolbarManagerService starting ExecuteAsync");

            // Wait a bit for all services to be initialized
            await Task.Delay(2000, stoppingToken);
            _logger.LogDebug("Initialisation delay completed. Resolving dependent services.");

            try
            {
                // Get services
                _scriptExecution = _serviceProvider.GetRequiredService<IScriptExecutionService>();
                _scriptStorage = _serviceProvider.GetRequiredService<IScriptStorageService>();
                _toolbarService = ActivatorUtilities.CreateInstance<DesktopToolbarService>(_serviceProvider);
                _logger.LogDebug("Resolved services: ScriptExecution={HasExecution}, ScriptStorage={HasStorage}, ToolbarService={HasToolbar}",
                    _scriptExecution != null,
                    _scriptStorage != null,
                    _toolbarService != null);

                if (_toolbarService != null)
                {
                    _toolbarService.ToolbarActionRequested += OnToolbarActionRequested;
                }

                // Subscribe to events
                _scriptExecution!.StateChanged += OnScriptStateChanged;

                _logger.LogInformation("Desktop toolbar manager started");
                _logger.LogInformation("Toolbar with global hotkeys (F9/F10/F11) will appear automatically when a script starts running");

                // Keep the service running
                bool hasLoggedNoActiveScripts = false;
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_scriptExecution == null || _scriptStorage == null)
                    {
                        _logger.LogWarning("Required services unavailable; skipping toolbar checks");
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    // Check for active executions and show/hide toolbar accordingly
                    var activeStates = _scriptExecution.GetAllExecutionStates()
                        .Where(s => s.Status == "Running" || s.Status == "Paused")
                        .ToList();

                    if (activeStates.Any())
                    {
                        hasLoggedNoActiveScripts = false;
                        var activeState = activeStates.First();
                        var script = await _scriptStorage.GetScriptAsync(activeState.ScriptId);
                        _logger.LogDebug("Active script detected: {ScriptName} - {Status}, showing toolbar", script?.Name ?? "Unknown", activeState.Status);
                        if (_toolbarService != null)
                        {
                            await _toolbarService.ShowToolbarAsync();
                            await _toolbarService.UpdateToolbarStateAsync(activeState, script);
                        }
                    }
                    else
                    {
                        if (!hasLoggedNoActiveScripts)
                        {
                            _logger.LogDebug("No active scripts, toolbar hidden. Start a script to see the toolbar.");
                            hasLoggedNoActiveScripts = true;
                        }
                        if (_toolbarService != null)
                        {
                            await _toolbarService.HideToolbarAsync();
                        }
                    }

                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in desktop toolbar manager");
            }
        }

        private async void OnScriptStateChanged(object? sender, ScriptExecutionState state)
        {
            try
            {
                _logger.LogInformation("Script state changed: {ScriptId} - {Status}", state.ScriptId, state.Status);
                
                if (_toolbarService == null || _scriptStorage == null)
                {
                    _logger.LogWarning("Toolbar service or storage service is null, cannot update toolbar");
                    return;
                }

                if (state.Status == "Running" || state.Status == "Paused")
                {
                    var script = await _scriptStorage.GetScriptAsync(state.ScriptId);
                    _logger.LogInformation("Showing toolbar for script: {ScriptName} ({Status})", script?.Name ?? "Unknown", state.Status);
                    await _toolbarService.ShowToolbarAsync();
                    await _toolbarService.UpdateToolbarStateAsync(state, script);
                }
                else
                {
                    _logger.LogInformation("Hiding toolbar - script status: {Status}", state.Status);
                    await _toolbarService.HideToolbarAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling script state change");
            }
        }

        private async void OnToolbarActionRequested(object? sender, string action)
        {
            try
            {
                if (_scriptExecution == null) return;

                var activeStates = _scriptExecution.GetAllExecutionStates()
                    .Where(s => s.Status == "Running" || s.Status == "Paused")
                    .ToList();

                if (!activeStates.Any()) return;

                var activeState = activeStates.First();

                switch (action)
                {
                    case "pause_resume":
                        if (activeState.Status == "Running")
                        {
                            _logger.LogInformation("Pause button clicked - pausing script {ScriptId}", activeState.ScriptId);
                            await _scriptExecution.PauseScriptAsync(activeState.ScriptId);
                            _logger.LogInformation("✓ Script paused via desktop toolbar");
                        }
                        else if (activeState.Status == "Paused")
                        {
                            _logger.LogInformation("Resume button clicked - resuming script {ScriptId}", activeState.ScriptId);
                            await _scriptExecution.ResumeScriptAsync(activeState.ScriptId);
                            _logger.LogInformation("✓ Script resumed via desktop toolbar");
                        }
                        break;
                    case "stop":
                        _logger.LogInformation("Stop button clicked - stopping script {ScriptId}", activeState.ScriptId);
                        await _scriptExecution.StopScriptAsync(activeState.ScriptId);
                        _logger.LogInformation("✓ Script stopped via desktop toolbar");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling toolbar action: {Action}", action);
            }
        }

        public override void Dispose()
        {
            _logger.LogInformation("Disposing DesktopToolbarManagerService...");
            
            try
            {
                if (_scriptExecution != null)
                {
                    _scriptExecution.StateChanged -= OnScriptStateChanged;
                }
                
                if (_toolbarService != null)
                {
                    _logger.LogDebug("Disposing toolbar service (includes hotkeys)...");
                    _toolbarService.ToolbarActionRequested -= OnToolbarActionRequested;
                    _toolbarService.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during DesktopToolbarManagerService disposal");
            }
            
            base.Dispose();
            _logger.LogInformation("DesktopToolbarManagerService disposed successfully");
        }
    }
}
