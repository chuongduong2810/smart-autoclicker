using System.Text.Json;
using AutomationTool.Models;
using System.Drawing;

namespace AutomationTool.Services
{
    public interface IScriptExecutionService
    {
        Task<string> StartScriptAsync(string scriptId);
        Task StopScriptAsync(string scriptId);
        Task PauseScriptAsync(string scriptId);
        Task ResumeScriptAsync(string scriptId);
        ScriptExecutionState? GetExecutionState(string scriptId);
        List<ScriptExecutionState> GetAllExecutionStates();
        event EventHandler<ExecutionLog>? LogGenerated;
        event EventHandler<ScriptExecutionState>? StateChanged;
    }

    public class ScriptExecutionService : IScriptExecutionService
    {
        private readonly IImageRecognitionService _imageRecognition;
        private readonly IAutomationEngine _automationEngine;
        private readonly IScreenshotService _screenshotService;
        private readonly IScriptStorageService _scriptStorage;
        private readonly ILogger<ScriptExecutionService> _logger;

        private readonly Dictionary<string, ScriptExecutionState> _executionStates;
        private readonly Dictionary<string, CancellationTokenSource> _cancellationTokens;
        private readonly Dictionary<string, Task> _executionTasks;

        public event EventHandler<ExecutionLog>? LogGenerated;
        public event EventHandler<ScriptExecutionState>? StateChanged;

        public ScriptExecutionService(
            IImageRecognitionService imageRecognition,
            IAutomationEngine automationEngine,
            IScreenshotService screenshotService,
            IScriptStorageService scriptStorage,
            ILogger<ScriptExecutionService> logger)
        {
            _imageRecognition = imageRecognition;
            _automationEngine = automationEngine;
            _screenshotService = screenshotService;
            _scriptStorage = scriptStorage;
            _logger = logger;

            _executionStates = new Dictionary<string, ScriptExecutionState>();
            _cancellationTokens = new Dictionary<string, CancellationTokenSource>();
            _executionTasks = new Dictionary<string, Task>();
        }

        public async Task<string> StartScriptAsync(string scriptId)
        {
            try
            {
                var script = await _scriptStorage.GetScriptAsync(scriptId);
                if (script == null)
                {
                    throw new ArgumentException($"Script with ID {scriptId} not found");
                }

                // Stop any existing execution
                if (_executionStates.ContainsKey(scriptId))
                {
                    await StopScriptAsync(scriptId);
                }

                // Create new execution state
                var executionState = new ScriptExecutionState
                {
                    ScriptId = scriptId,
                    StartTime = DateTime.Now,
                    Status = ExecutionStatus.Running.ToString(),
                    CurrentStepId = script.Steps.FirstOrDefault()?.Id ?? string.Empty
                };

                _executionStates[scriptId] = executionState;

                // Create cancellation token
                var cancellationTokenSource = new CancellationTokenSource();
                _cancellationTokens[scriptId] = cancellationTokenSource;

                // Start execution task
                var executionTask = ExecuteScriptAsync(script, executionState, cancellationTokenSource.Token);
                _executionTasks[scriptId] = executionTask;

                OnStateChanged(executionState);
                LogExecution(scriptId, string.Empty, AutomationTool.Models.LogLevel.Info, $"Script '{script.Name}' started");

                return "Script execution started successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting script {ScriptId}", scriptId);
                throw;
            }
        }

        public async Task StopScriptAsync(string scriptId)
        {
            try
            {
                if (_cancellationTokens.TryGetValue(scriptId, out var cancellationTokenSource))
                {
                    cancellationTokenSource.Cancel();
                    
                    if (_executionTasks.TryGetValue(scriptId, out var task))
                    {
                        await task;
                        _executionTasks.Remove(scriptId);
                    }
                    
                    _cancellationTokens.Remove(scriptId);
                }

                if (_executionStates.TryGetValue(scriptId, out var state))
                {
                    state.Status = ExecutionStatus.Stopped.ToString();
                    OnStateChanged(state);
                    LogExecution(scriptId, string.Empty, Models.LogLevel.Info, "Script execution stopped");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping script {ScriptId}", scriptId);
                throw;
            }
        }

        public async Task PauseScriptAsync(string scriptId)
        {
            try
            {
                if (_executionStates.TryGetValue(scriptId, out var state))
                {
                    state.Status = ExecutionStatus.Paused.ToString();
                    OnStateChanged(state);
                    LogExecution(scriptId, string.Empty, Models.LogLevel.Info, "Script execution paused");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing script {ScriptId}", scriptId);
                throw;
            }
        }

        public async Task ResumeScriptAsync(string scriptId)
        {
            try
            {
                if (_executionStates.TryGetValue(scriptId, out var state))
                {
                    state.Status = ExecutionStatus.Running.ToString();
                    OnStateChanged(state);
                    LogExecution(scriptId, string.Empty, Models.LogLevel.Info, "Script execution resumed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming script {ScriptId}", scriptId);
                throw;
            }
        }

        public ScriptExecutionState? GetExecutionState(string scriptId)
        {
            return _executionStates.TryGetValue(scriptId, out var state) ? state : null;
        }

        public List<ScriptExecutionState> GetAllExecutionStates()
        {
            return _executionStates.Values.ToList();
        }

        private async Task ExecuteScriptAsync(AutomationScript script, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            try
            {
                var currentStepIndex = 0;
                var maxIterations = 1000; // Prevent infinite loops
                var iterations = 0;

                while (currentStepIndex < script.Steps.Count && iterations < maxIterations && !cancellationToken.IsCancellationRequested)
                {
                    // Check if paused
                    while (state.Status == ExecutionStatus.Paused.ToString() && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(100, cancellationToken);
                    }

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var step = script.Steps[currentStepIndex];
                    
                    if (!step.IsEnabled)
                    {
                        currentStepIndex++;
                        continue;
                    }

                    state.CurrentStepId = step.Id;
                    OnStateChanged(state);

                    LogExecution(script.Id, step.Id, Models.LogLevel.Info, $"Executing step: {step.Name}");

                    try
                    {
                        var result = await ExecuteStepAsync(step, state, cancellationToken);
                        
                        if (result.Success)
                        {
                            if (!string.IsNullOrEmpty(result.NextStepId))
                            {
                                // Jump to specific step
                                var nextStepIndex = script.Steps.FindIndex(s => s.Id == result.NextStepId);
                                if (nextStepIndex >= 0)
                                {
                                    currentStepIndex = nextStepIndex;
                                    LogExecution(script.Id, step.Id, Models.LogLevel.Info, $"Jumping to step: {script.Steps[nextStepIndex].Name}");
                                }
                                else
                                {
                                    LogExecution(script.Id, step.Id, Models.LogLevel.Warning, $"Step ID {result.NextStepId} not found, continuing to next step");
                                    currentStepIndex++;
                                }
                            }
                            else
                            {
                                currentStepIndex++;
                            }
                        }
                        else
                        {
                            // Handle failure based on step configuration
                            if (!string.IsNullOrEmpty(step.ElseStepId))
                            {
                                var elseStepIndex = script.Steps.FindIndex(s => s.Id == step.ElseStepId);
                                if (elseStepIndex >= 0)
                                {
                                    currentStepIndex = elseStepIndex;
                                    LogExecution(script.Id, step.Id, Models.LogLevel.Info, $"Condition failed, jumping to else step: {script.Steps[elseStepIndex].Name}");
                                }
                                else
                                {
                                    LogExecution(script.Id, step.Id, Models.LogLevel.Warning, $"Else step ID {step.ElseStepId} not found, continuing to next step");
                                    currentStepIndex++;
                                }
                            }
                            else
                            {
                                currentStepIndex++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogExecution(script.Id, step.Id, Models.LogLevel.Error, $"Error executing step: {ex.Message}");
                        
                        if (!string.IsNullOrEmpty(step.ElseStepId))
                        {
                            var elseStepIndex = script.Steps.FindIndex(s => s.Id == step.ElseStepId);
                            if (elseStepIndex >= 0)
                            {
                                currentStepIndex = elseStepIndex;
                            }
                            else
                            {
                                currentStepIndex++;
                            }
                        }
                        else
                        {
                            currentStepIndex++;
                        }
                    }

                    iterations++;
                }

                // Script completed
                state.Status = ExecutionStatus.Completed.ToString();
                LogExecution(script.Id, string.Empty, Models.LogLevel.Info, "Script execution completed");
            }
            catch (OperationCanceledException)
            {
                state.Status = ExecutionStatus.Stopped.ToString();
                LogExecution(script.Id, string.Empty, Models.LogLevel.Info, "Script execution cancelled");
            }
            catch (Exception ex)
            {
                state.Status = ExecutionStatus.Error.ToString();
                LogExecution(script.Id, string.Empty, Models.LogLevel.Error, $"Script execution failed: {ex.Message}");
                _logger.LogError(ex, "Error executing script {ScriptId}", script.Id);
            }
            finally
            {
                OnStateChanged(state);
            }
        }

        private async Task<StepExecutionResult> ExecuteStepAsync(ScriptStep step, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            switch (step.Type.ToLower())
            {
                case "condition":
                    return await ExecuteConditionStepAsync(step, state, cancellationToken);
                case "action":
                    return await ExecuteActionStepAsync(step, state, cancellationToken);
                case "wait":
                    return await ExecuteWaitStepAsync(step, state, cancellationToken);
                case "jump":
                    return await ExecuteJumpStepAsync(step, state, cancellationToken);
                default:
                    LogExecution(state.ScriptId, step.Id, Models.LogLevel.Warning, $"Unknown step type: {step.Type}");
                    return new StepExecutionResult { Success = true };
            }
        }

        private async Task<StepExecutionResult> ExecuteConditionStepAsync(ScriptStep step, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            var allConditionsMet = true;
            
            foreach (var condition in step.Conditions)
            {
                var conditionMet = await EvaluateConditionAsync(condition, state, cancellationToken);
                
                if (condition.Operator.ToUpper() == "OR")
                {
                    if (conditionMet)
                    {
                        allConditionsMet = true;
                        break;
                    }
                }
                else // AND (default)
                {
                    if (!conditionMet)
                    {
                        allConditionsMet = false;
                        break;
                    }
                }
            }

            if (allConditionsMet)
            {
                // Execute actions
                foreach (var action in step.Actions)
                {
                    await ExecuteActionAsync(action, state, cancellationToken);
                }
            }

            return new StepExecutionResult { Success = allConditionsMet };
        }

        private async Task<bool> EvaluateConditionAsync(ScriptCondition condition, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            switch (condition.Type.ToLower())
            {
                case "image_found":
                    return await EvaluateImageFoundConditionAsync(condition, state, cancellationToken);
                case "image_not_found":
                    return !(await EvaluateImageFoundConditionAsync(condition, state, cancellationToken));
                case "timeout":
                    return await EvaluateTimeoutConditionAsync(condition, state, cancellationToken);
                case "always":
                    return true;
                case "never":
                    return false;
                default:
                    LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Warning, $"Unknown condition type: {condition.Type}");
                    return false;
            }
        }

        private async Task<bool> EvaluateImageFoundConditionAsync(ScriptCondition condition, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            try
            {
                var templateImageId = condition.Parameters.GetValueOrDefault("templateImageId")?.ToString();
                var threshold = Convert.ToDouble(condition.Parameters.GetValueOrDefault("threshold", 0.8));

                if (string.IsNullOrEmpty(templateImageId))
                {
                    LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Warning, "Template image ID not specified in condition");
                    return false;
                }

                var templateImage = await _scriptStorage.GetTemplateImageAsync(templateImageId);
                if (templateImage == null)
                {
                    LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Warning, $"Template image {templateImageId} not found");
                    return false;
                }

                var screenData = await _screenshotService.CaptureFullScreenAsync();
                var result = await _imageRecognition.FindImageAsync(screenData, templateImage.ImageData, threshold);

                LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Debug, 
                    $"Image search result: Found={result.Found}, Confidence={result.Confidence:F3}");

                return result.Found;
            }
            catch (Exception ex)
            {
                LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Error, $"Error evaluating image condition: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> EvaluateTimeoutConditionAsync(ScriptCondition condition, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            var timeoutMs = Convert.ToInt32(condition.Parameters.GetValueOrDefault("timeoutMs", 5000));
            var elapsedMs = (DateTime.Now - state.StartTime).TotalMilliseconds;
            
            return elapsedMs >= timeoutMs;
        }

        private async Task<StepExecutionResult> ExecuteActionStepAsync(ScriptStep step, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            foreach (var action in step.Actions)
            {
                await ExecuteActionAsync(action, state, cancellationToken);
            }

            return new StepExecutionResult { Success = true };
        }

        private async Task ExecuteActionAsync(ScriptAction action, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            try
            {
                switch (action.Type.ToLower())
                {
                    case "click":
                        await ExecuteClickActionAsync(action, state, cancellationToken);
                        break;
                    case "double_click":
                        await ExecuteDoubleClickActionAsync(action, state, cancellationToken);
                        break;
                    case "right_click":
                        await ExecuteRightClickActionAsync(action, state, cancellationToken);
                        break;
                    case "type":
                        await ExecuteTypeActionAsync(action, state, cancellationToken);
                        break;
                    case "key_press":
                        await ExecuteKeyPressActionAsync(action, state, cancellationToken);
                        break;
                    case "wait":
                        await ExecuteWaitActionAsync(action, state, cancellationToken);
                        break;
                    case "screenshot":
                        await ExecuteScreenshotActionAsync(action, state, cancellationToken);
                        break;
                    default:
                        LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Warning, $"Unknown action type: {action.Type}");
                        break;
                }

                // Wait after action if specified
                if (action.DelayAfter > 0)
                {
                    await Task.Delay(action.DelayAfter, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Error, $"Error executing action {action.Type}: {ex.Message}");
                throw;
            }
        }

        private async Task ExecuteClickActionAsync(ScriptAction action, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            var x = Convert.ToInt32(action.Parameters.GetValueOrDefault("x", 0));
            var y = Convert.ToInt32(action.Parameters.GetValueOrDefault("y", 0));
            
            await _automationEngine.ClickAsync(x, y);
            LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Info, $"Clicked at ({x}, {y})");
        }

        private async Task ExecuteDoubleClickActionAsync(ScriptAction action, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            var x = Convert.ToInt32(action.Parameters.GetValueOrDefault("x", 0));
            var y = Convert.ToInt32(action.Parameters.GetValueOrDefault("y", 0));
            
            await _automationEngine.DoubleClickAsync(new Point(x, y));
            LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Info, $"Double-clicked at ({x}, {y})");
        }

        private async Task ExecuteRightClickActionAsync(ScriptAction action, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            var x = Convert.ToInt32(action.Parameters.GetValueOrDefault("x", 0));
            var y = Convert.ToInt32(action.Parameters.GetValueOrDefault("y", 0));
            
            await _automationEngine.RightClickAsync(new Point(x, y));
            LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Info, $"Right-clicked at ({x}, {y})");
        }

        private async Task ExecuteTypeActionAsync(ScriptAction action, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            var text = action.Parameters.GetValueOrDefault("text")?.ToString() ?? string.Empty;
            
            await _automationEngine.TypeTextAsync(text);
            LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Info, $"Typed text: {text}");
        }

        private async Task ExecuteKeyPressActionAsync(ScriptAction action, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            var keys = action.Parameters.GetValueOrDefault("keys")?.ToString() ?? string.Empty;
            
            await _automationEngine.SendKeysAsync(keys);
            LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Info, $"Sent keys: {keys}");
        }

        private async Task ExecuteWaitActionAsync(ScriptAction action, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            var milliseconds = Convert.ToInt32(action.Parameters.GetValueOrDefault("milliseconds", 1000));
            
            await _automationEngine.WaitAsync(milliseconds);
            LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Info, $"Waited {milliseconds}ms");
        }

        private async Task ExecuteScreenshotActionAsync(ScriptAction action, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            var fileName = action.Parameters.GetValueOrDefault("fileName")?.ToString() ?? $"script_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}";
            
            var screenData = await _screenshotService.CaptureFullScreenAsync();
            var filePath = await _screenshotService.SaveScreenshotAsync(screenData, fileName);
            
            LogExecution(state.ScriptId, string.Empty, Models.LogLevel.Info, $"Screenshot saved: {filePath}");
        }

        private async Task<StepExecutionResult> ExecuteWaitStepAsync(ScriptStep step, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            var milliseconds = Convert.ToInt32(step.Parameters.GetValueOrDefault("milliseconds", 1000));
            await Task.Delay(milliseconds, cancellationToken);
            
            return new StepExecutionResult { Success = true };
        }

        private async Task<StepExecutionResult> ExecuteJumpStepAsync(ScriptStep step, ScriptExecutionState state, CancellationToken cancellationToken)
        {
            var targetStepId = step.Parameters.GetValueOrDefault("targetStepId")?.ToString();
            
            return new StepExecutionResult 
            { 
                Success = true, 
                NextStepId = targetStepId 
            };
        }

        private void LogExecution(string scriptId, string stepId, Models.LogLevel level, string message)
        {
            var log = new ExecutionLog
            {
                ScriptId = scriptId,
                StepId = stepId,
                Level = level.ToString().ToUpper(),
                Message = message,
                Timestamp = DateTime.Now
            };

            if (_executionStates.TryGetValue(scriptId, out var state))
            {
                state.Logs.Add(log);
                
                // Keep only last 1000 logs to prevent memory issues
                if (state.Logs.Count > 1000)
                {
                    state.Logs.RemoveRange(0, state.Logs.Count - 1000);
                }
            }

            OnLogGenerated(log);
        }

        private void OnLogGenerated(ExecutionLog log)
        {
            LogGenerated?.Invoke(this, log);
        }

        private void OnStateChanged(ScriptExecutionState state)
        {
            StateChanged?.Invoke(this, state);
        }

        private class StepExecutionResult
        {
            public bool Success { get; set; }
            public string? NextStepId { get; set; }
        }
    }

    public interface IScriptStorageService
    {
        Task<AutomationScript?> GetScriptAsync(string scriptId);
        Task<List<AutomationScript>> GetAllScriptsAsync();
        Task<string> SaveScriptAsync(AutomationScript script);
        Task DeleteScriptAsync(string scriptId);
        Task<TemplateImage?> GetTemplateImageAsync(string templateImageId);
        Task<List<TemplateImage>> GetAllTemplateImagesAsync();
        Task<string> SaveTemplateImageAsync(TemplateImage templateImage);
        Task DeleteTemplateImageAsync(string templateImageId);
    }
}