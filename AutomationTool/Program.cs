using AutomationTool.Components;
using AutomationTool.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register automation services
builder.Services.AddSingleton<IScreenshotService, ScreenshotService>();
builder.Services.AddSingleton<IImageRecognitionService, ImageRecognitionService>();
builder.Services.AddSingleton<IAutomationEngine, WindowsAutomationEngine>();
builder.Services.AddSingleton<IScriptStorageService, FileBasedScriptStorageService>();
builder.Services.AddSingleton<IScriptExecutionService, ScriptExecutionService>();
// Desktop window service is created directly in the manager

// Add background services
builder.Services.AddHostedService<DesktopToolbarManagerService>();

// Add logging
builder.Services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
