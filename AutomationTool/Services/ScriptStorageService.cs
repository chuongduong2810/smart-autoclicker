using System.Text.Json;
using AutomationTool.Models;
using Microsoft.Extensions.Logging;

namespace AutomationTool.Services
{
    public class FileBasedScriptStorageService : IScriptStorageService
    {
        private readonly string _scriptsPath;
        private readonly string _templatesPath;
        private readonly ILogger<FileBasedScriptStorageService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public FileBasedScriptStorageService(ILogger<FileBasedScriptStorageService> logger)
        {
            _logger = logger;
            _scriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Scripts");
            _templatesPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Templates");

            // Ensure directories exist
            Directory.CreateDirectory(_scriptsPath);
            Directory.CreateDirectory(_templatesPath);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<AutomationScript?> GetScriptAsync(string scriptId)
        {
            try
            {
                var filePath = Path.Combine(_scriptsPath, $"{scriptId}.json");
                
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Script file not found: {FilePath}", filePath);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var script = JsonSerializer.Deserialize<AutomationScript>(json, _jsonOptions);
                
                _logger.LogDebug("Loaded script: {ScriptId}", scriptId);
                return script;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading script: {ScriptId}", scriptId);
                return null;
            }
        }

        public async Task<List<AutomationScript>> GetAllScriptsAsync()
        {
            try
            {
                var scripts = new List<AutomationScript>();
                var files = Directory.GetFiles(_scriptsPath, "*.json");

                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var script = JsonSerializer.Deserialize<AutomationScript>(json, _jsonOptions);
                        
                        if (script != null)
                        {
                            scripts.Add(script);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error loading script file: {File}", file);
                    }
                }

                _logger.LogDebug("Loaded {Count} scripts", scripts.Count);
                return scripts.OrderBy(s => s.Name).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading scripts");
                return new List<AutomationScript>();
            }
        }

        public async Task<string> SaveScriptAsync(AutomationScript script)
        {
            try
            {
                script.ModifiedAt = DateTime.Now;
                
                var filePath = Path.Combine(_scriptsPath, $"{script.Id}.json");
                var json = JsonSerializer.Serialize(script, _jsonOptions);
                
                await File.WriteAllTextAsync(filePath, json);
                
                _logger.LogInformation("Saved script: {ScriptId} to {FilePath}", script.Id, filePath);
                return script.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving script: {ScriptId}", script.Id);
                throw;
            }
        }

        public Task DeleteScriptAsync(string scriptId)
        {
            try
            {
                var filePath = Path.Combine(_scriptsPath, $"{scriptId}.json");
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted script: {ScriptId}", scriptId);
                }
                else
                {
                    _logger.LogWarning("Script file not found for deletion: {ScriptId}", scriptId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting script: {ScriptId}", scriptId);
                throw;
            }

            return Task.CompletedTask;
        }

        public async Task<TemplateImage?> GetTemplateImageAsync(string templateImageId)
        {
            try
            {
                var filePath = Path.Combine(_templatesPath, $"{templateImageId}.json");
                
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Template image file not found: {FilePath}", filePath);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var template = JsonSerializer.Deserialize<TemplateImage>(json, _jsonOptions);
                
                // Load image data if file path exists
                if (template != null && !string.IsNullOrEmpty(template.FilePath) && File.Exists(template.FilePath))
                {
                    template.ImageData = await File.ReadAllBytesAsync(template.FilePath);
                }
                
                _logger.LogDebug("Loaded template image: {TemplateImageId}", templateImageId);
                return template;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading template image: {TemplateImageId}", templateImageId);
                return null;
            }
        }

        public async Task<List<TemplateImage>> GetAllTemplateImagesAsync()
        {
            try
            {
                var templates = new List<TemplateImage>();
                var files = Directory.GetFiles(_templatesPath, "*.json");

                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var template = JsonSerializer.Deserialize<TemplateImage>(json, _jsonOptions);
                        
                        if (template != null)
                        {
                            // Load image data if file path exists
                            if (!string.IsNullOrEmpty(template.FilePath) && File.Exists(template.FilePath))
                            {
                                template.ImageData = await File.ReadAllBytesAsync(template.FilePath);
                            }
                            
                            templates.Add(template);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error loading template image file: {File}", file);
                    }
                }

                _logger.LogDebug("Loaded {Count} template images", templates.Count);
                return templates.OrderBy(t => t.Name).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading template images");
                return new List<TemplateImage>();
            }
        }

        public async Task<string> SaveTemplateImageAsync(TemplateImage templateImage)
        {
            try
            {
                templateImage.CreatedAt = DateTime.Now;
                
                // Save image data to file if provided
                if (templateImage.ImageData != null && templateImage.ImageData.Length > 0)
                {
                    var imageFileName = $"{templateImage.Id}.png";
                    var imagePath = Path.Combine(_templatesPath, imageFileName);
                    await File.WriteAllBytesAsync(imagePath, templateImage.ImageData);
                    templateImage.FilePath = imagePath;
                }
                
                // Save template metadata
                var metadataFilePath = Path.Combine(_templatesPath, $"{templateImage.Id}.json");
                
                // Create a copy without image data for serialization (to keep files smaller)
                var templateForSerialization = new TemplateImage
                {
                    Id = templateImage.Id,
                    Name = templateImage.Name,
                    FilePath = templateImage.FilePath,
                    CreatedAt = templateImage.CreatedAt,
                    CaptureRegion = templateImage.CaptureRegion,
                    MatchThreshold = templateImage.MatchThreshold,
                    ImageData = Array.Empty<byte>() // Don't serialize the actual image data
                };
                
                var json = JsonSerializer.Serialize(templateForSerialization, _jsonOptions);
                await File.WriteAllTextAsync(metadataFilePath, json);
                
                _logger.LogInformation("Saved template image: {TemplateImageId} to {FilePath}", templateImage.Id, metadataFilePath);
                return templateImage.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving template image: {TemplateImageId}", templateImage.Id);
                throw;
            }
        }

        public Task DeleteTemplateImageAsync(string templateImageId)
        {
            try
            {
                var metadataFilePath = Path.Combine(_templatesPath, $"{templateImageId}.json");
                var imageFilePath = Path.Combine(_templatesPath, $"{templateImageId}.png");
                
                if (File.Exists(metadataFilePath))
                {
                    File.Delete(metadataFilePath);
                    _logger.LogDebug("Deleted template metadata: {TemplateImageId}", templateImageId);
                }
                
                if (File.Exists(imageFilePath))
                {
                    File.Delete(imageFilePath);
                    _logger.LogDebug("Deleted template image file: {TemplateImageId}", templateImageId);
                }
                
                _logger.LogInformation("Deleted template image: {TemplateImageId}", templateImageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting template image: {TemplateImageId}", templateImageId);
                throw;
            }

            return Task.CompletedTask;
        }
    }

    // In-memory storage service for testing and development
    public class InMemoryScriptStorageService : IScriptStorageService
    {
        private readonly Dictionary<string, AutomationScript> _scripts;
        private readonly Dictionary<string, TemplateImage> _templateImages;
        private readonly ILogger<InMemoryScriptStorageService> _logger;

        public InMemoryScriptStorageService(ILogger<InMemoryScriptStorageService> logger)
        {
            _logger = logger;
            _scripts = new Dictionary<string, AutomationScript>();
            _templateImages = new Dictionary<string, TemplateImage>();
            
            // Add some sample data
            InitializeSampleData();
        }

        public Task<AutomationScript?> GetScriptAsync(string scriptId)
        {
            _scripts.TryGetValue(scriptId, out var script);
            return Task.FromResult(script);
        }

        public Task<List<AutomationScript>> GetAllScriptsAsync()
        {
            return Task.FromResult(_scripts.Values.OrderBy(s => s.Name).ToList());
        }

        public Task<string> SaveScriptAsync(AutomationScript script)
        {
            script.ModifiedAt = DateTime.Now;
            _scripts[script.Id] = script;
            _logger.LogInformation("Saved script in memory: {ScriptId}", script.Id);
            return Task.FromResult(script.Id);
        }

        public Task DeleteScriptAsync(string scriptId)
        {
            _scripts.Remove(scriptId);
            _logger.LogInformation("Deleted script from memory: {ScriptId}", scriptId);
            return Task.CompletedTask;
        }

        public Task<TemplateImage?> GetTemplateImageAsync(string templateImageId)
        {
            _templateImages.TryGetValue(templateImageId, out var template);
            return Task.FromResult(template);
        }

        public Task<List<TemplateImage>> GetAllTemplateImagesAsync()
        {
            return Task.FromResult(_templateImages.Values.OrderBy(t => t.Name).ToList());
        }

        public Task<string> SaveTemplateImageAsync(TemplateImage templateImage)
        {
            templateImage.CreatedAt = DateTime.Now;
            _templateImages[templateImage.Id] = templateImage;
            _logger.LogInformation("Saved template image in memory: {TemplateImageId}", templateImage.Id);
            return Task.FromResult(templateImage.Id);
        }

        public Task DeleteTemplateImageAsync(string templateImageId)
        {
            _templateImages.Remove(templateImageId);
            _logger.LogInformation("Deleted template image from memory: {TemplateImageId}", templateImageId);
            return Task.CompletedTask;
        }

        private void InitializeSampleData()
        {
            // Sample script
            var sampleScript = new AutomationScript
            {
                Id = "sample-script-1",
                Name = "Sample Click Script",
                Description = "A sample script that demonstrates basic clicking",
                Steps = new List<ScriptStep>
                {
                    new ScriptStep
                    {
                        Id = "step-1",
                        Order = 1,
                        Type = "action",
                        Name = "Click at center",
                        Actions = new List<ScriptAction>
                        {
                            new ScriptAction
                            {
                                Type = "click",
                                Parameters = new Dictionary<string, object>
                                {
                                    { "x", 960 },
                                    { "y", 540 }
                                }
                            }
                        }
                    },
                    new ScriptStep
                    {
                        Id = "step-2",
                        Order = 2,
                        Type = "wait",
                        Name = "Wait 2 seconds",
                        Parameters = new Dictionary<string, object>
                        {
                            { "milliseconds", 2000 }
                        }
                    }
                }
            };

            _scripts[sampleScript.Id] = sampleScript;
            _logger.LogInformation("Initialized sample data with {ScriptCount} scripts and {TemplateCount} templates", 
                _scripts.Count, _templateImages.Count);
        }
    }
}