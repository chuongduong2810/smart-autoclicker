using System.Diagnostics;
using AutomationTool.Services;
using AutomationTool.Utils.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutomationTool;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var services = new ServiceCollection();
        Debug.WriteLine("[Startup] Configuring services...");
        ConfigureServices(services);

        var serviceProvider = services.BuildServiceProvider();
        Debug.WriteLine("[Startup] Service provider built.");

        using var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var startupLogger = loggerFactory.CreateLogger("Startup");
        startupLogger.LogInformation("Application starting up at {Timestamp}", DateTimeOffset.Now);

        // Start the background services
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        foreach (var hostedService in hostedServices)
        {
            startupLogger.LogDebug("Starting hosted service {Service}", hostedService.GetType().Name);
            _ = hostedService.StartAsync(CancellationToken.None);
        }

        startupLogger.LogInformation("Launching MainForm and Blazor WebView");
        Application.Run(new MainForm(serviceProvider));
        startupLogger.LogInformation("MainForm closed. Initiating shutdown sequence");

        // Stop the background services on shutdown
        foreach (var hostedService in hostedServices)
        {
            startupLogger.LogDebug("Stopping hosted service {Service}", hostedService.GetType().Name);
            _ = hostedService.StopAsync(CancellationToken.None);
        }

        startupLogger.LogInformation("Application shutdown complete at {Timestamp}", DateTimeOffset.Now);
    }

    static void ConfigureServices(ServiceCollection services)
    {
        // Add Windows Forms Blazor WebView services
        services.AddWindowsFormsBlazorWebView();

#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        // Register automation services
        services.AddSingleton<IScreenshotService, ScreenshotService>();
        services.AddSingleton<IImageRecognitionService, ImageRecognitionService>();
        services.AddSingleton<IAutomationEngine, WindowsAutomationEngine>();
        services.AddSingleton<IScriptStorageService, FileBasedScriptStorageService>();
        services.AddSingleton<IScriptExecutionService, ScriptExecutionService>();
        services.AddSingleton<IWindowEnumerationService, DesktopWindowService>();
        
        // Add background services
        services.AddSingleton<IHostedService, DesktopToolbarManagerService>();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddDebug();

            try
            {
                var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDirectory);
                var logFilePath = Path.Combine(logDirectory, $"automationtool_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                builder.AddProvider(new FileLoggerProvider(logFilePath, LogLevel.Debug));
                builder.AddConsole();
            }
            catch (Exception ex)
            {
                builder.AddConsole();
                Debug.WriteLine($"[Logging] Failed to initialise file logger provider: {ex}");
            }
        });
    }
}
