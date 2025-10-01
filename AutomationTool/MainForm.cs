using AutomationTool.Services;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutomationTool;

public partial class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainForm> _logger;

    public MainForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = _serviceProvider.GetRequiredService<ILogger<MainForm>>();

        _logger.LogInformation("MainForm initialising - starting constructor");

        try
        {
        InitializeComponent();
        
        SetupBlazorWebView();
        
        // Configure the form
        this.Text = "Smart Auto Clicker";
        this.Size = new Size(1200, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(800, 600);
            _logger.LogInformation("MainForm initialised successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initialising MainForm");
            throw;
        }
    }

    private void SetupBlazorWebView()
    {
        _logger.LogDebug("Setting up BlazorWebView host page and services");

        blazorWebView1.HostPage = "wwwroot/index.html";
        blazorWebView1.Services = _serviceProvider;
        blazorWebView1.RootComponents.Add<Components.App>("#app");
        this.Controls.Add(blazorWebView1);

        blazorWebView1.BlazorWebViewInitializing += (_, args) =>
        {
            _logger.LogDebug("BlazorWebViewInitializing: userDataFolder={UserDataFolder}", args.UserDataFolder);
        };

        blazorWebView1.BlazorWebViewInitialized += (_, _) =>
        {
            _logger.LogInformation("BlazorWebView initialised successfully. Starting Blazor.");
        };

        blazorWebView1.UrlLoading += (_, args) =>
        {
            _logger.LogDebug("UrlLoading: {Url} - kind: {Kind}", args.Url, args.UrlLoadingStrategy);
        };
    }
}


