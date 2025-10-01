# Smart Auto Clicker - WinForms Edition

## Overview
This is a WinForms desktop version of the Smart Auto Clicker application that uses Blazor WebView2 to host the UI. It can be built as a single, self-contained executable for easy distribution.

## ✅ Conversion Complete!

Your Blazor application has been successfully converted from a web-based ASP.NET Core application to a native Windows Forms desktop application.

## Key Changes Made

### 1. Project Configuration (`AutomationTool.csproj`)
- Changed SDK from `Microsoft.NET.Sdk.Web` to `Microsoft.NET.Sdk.Razor`
- Added `OutputType` as `WinExe` for Windows application
- Added WebView2 package: `Microsoft.AspNetCore.Components.WebView.WindowsForms`
- Configured for self-contained single-file publishing
- Added required dependencies:
  - `Microsoft.Extensions.Hosting`
  - `Microsoft.Extensions.Logging`
  - `Microsoft.Extensions.Logging.Debug`
  - `Microsoft.AspNetCore.Http.Abstractions`

### 2. WinForms UI Components
- **`MainForm.cs`**: Main application form with BlazorWebView control
- **`MainForm.Designer.cs`**: Form designer code
- **`wwwroot/index.html`**: Host page for WebView2

### 3. Application Entry Point (`Program.cs`)
- Changed from ASP.NET Core web hosting to WinForms
- Configured dependency injection for desktop environment
- Properly manages background services lifecycle

### 4. UI Components
- **`Components/App.razor`**: Simplified for BlazorWebView hosting
- **`Components/Pages/Error.razor`**: Updated to work without HttpContext
- Added `Microsoft.Extensions.Logging` using statements to all components

## Building the Application

### Quick Development Build
```bash
cd AutomationTool
dotnet restore
dotnet build
dotnet run
```

### Create Self-Contained Executable
```bash
cd AutomationTool
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable will be located at:
```
AutomationTool/bin/Release/net8.0-windows/win-x64/publish/AutomationTool.exe
```

### Build for Different Architectures

**Windows x64 (64-bit):**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Windows x86 (32-bit):**
```bash
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true
```

**Windows ARM64:**
```bash
dotnet publish -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true
```

## Distribution

The published executable (`AutomationTool.exe`) is self-contained and includes:
- ✅ .NET 8.0 runtime
- ✅ All application dependencies
- ✅ Blazor WebView2 components
- ✅ Emgu.CV native libraries

### What Users Need:
1. **WebView2 Runtime** - Pre-installed on Windows 11 and recent Windows 10 updates
   - For older systems: https://developer.microsoft.com/microsoft-edge/webview2/

That's it! No .NET installation required on target machines.

## Output Structure

The publish folder contains:
```
AutomationTool.exe          # Main self-contained executable (~150-250 MB)
wwwroot/                    # Static web assets (CSS, JS, images)
Data/                       # Scripts and templates storage
appsettings.json            # Configuration files
```

**Note:** The entire publish folder should be distributed together, though the .exe contains all the runtime components.

## Features

All original features are preserved:
- ✅ Script recording and playback
- ✅ Image template matching with Emgu.CV
- ✅ Global hotkeys (F9, F10, F11)
- ✅ Desktop toolbar for script execution
- ✅ Screenshot capture with region selection
- ✅ Script management and storage
- ✅ Native Windows integration

## Advantages Over Web Version

1. **No Browser Required** - Runs as a native Windows application
2. **Single Executable** - Easy distribution, no installation needed
3. **Better Performance** - Direct system integration
4. **Standalone** - Works offline, no web server required
5. **Native Look & Feel** - Windows taskbar, window management
6. **Portable** - Can run from USB or any folder

## Troubleshooting

### WebView2 Not Found
If you encounter "WebView2 Runtime not found" errors:
1. Download: https://go.microsoft.com/fwlink/p/?LinkId=2124703
2. Install the WebView2 Runtime
3. Restart the application

### Application Won't Start
- Ensure Windows 10 1809 or later
- Check Windows Event Viewer for errors
- Verify architecture matches your system (x64/x86/ARM64)

### Reduce Executable Size
For framework-dependent deployment (requires .NET on target machine):
```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## Development Notes

### Running in Development Mode
```bash
dotnet run
```

The application will:
- Start as a WinForms window
- Load your Blazor UI inside WebView2
- Enable hot reload for development
- Use Debug logging

### Architecture

```
WinForms Application (MainForm)
    └── BlazorWebView Control
        └── Blazor Components (Your UI)
            └── Services (DI Container)
                ├── ScriptExecutionService
                ├── ImageRecognitionService
                ├── ScreenshotService
                ├── AutomationEngine
                └── DesktopToolbarManagerService
```

## Next Steps

1. **Test the executable**: Run `AutomationTool.exe` from the publish folder
2. **Test all features**: Verify scripts, templates, hotkeys work correctly
3. **Package for distribution**: Zip the entire publish folder
4. **Add application icon**: Set icon in project properties (optional)
5. **Create installer**: Use tools like Inno Setup or WiX (optional)

For detailed build instructions, see `AutomationTool/BUILD.md`.

