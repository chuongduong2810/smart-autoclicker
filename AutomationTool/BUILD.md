# Building the Smart Auto Clicker - WinForms Edition

## Overview
This guide explains how to build the Smart Auto Clicker as a single, self-contained executable for Windows.

## Prerequisites
- .NET 8.0 SDK or later
- Windows 10/11

## Building the Application

### Development Build
For development and testing:

```bash
cd AutomationTool
dotnet restore
dotnet build
```

Run the application:
```bash
dotnet run
```

### Release Build (Self-Contained Executable)

To create a single, self-contained executable for **Windows x64**:

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

## Build Configuration

The project is configured with the following settings for self-contained deployment:

- **PublishSingleFile**: Creates a single executable file
- **SelfContained**: Includes the .NET runtime (no need to install .NET on target machine)
- **IncludeNativeLibrariesForSelfExtract**: Includes native libraries (Emgu.CV)
- **EnableCompressionInSingleFile**: Compresses the executable
- **PublishTrimmed**: Disabled to ensure full compatibility with Blazor and reflection

## Output Size

The final executable will be approximately 150-250 MB due to:
- .NET runtime
- Blazor WebView2 components
- Emgu.CV native libraries
- Application code and assets

## Distribution

The published executable (`AutomationTool.exe`) can be distributed as a standalone file. Users do not need:
- .NET runtime installed
- Any additional dependencies

**Note**: WebView2 Runtime must be installed on the target machine. It's pre-installed on Windows 11 and recent Windows 10 updates. For older systems, it can be downloaded from: https://developer.microsoft.com/microsoft-edge/webview2/

## Troubleshooting

### WebView2 Not Found
If users encounter "WebView2 Runtime not found" errors:
1. Download and install WebView2 Runtime: https://go.microsoft.com/fwlink/p/?LinkId=2124703
2. Restart the application

### Large File Size
To reduce executable size, you can:
1. Use framework-dependent deployment (requires .NET installed on target machine):
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
   ```

### Application Won't Start
- Ensure you're running on Windows 10 1809 or later
- Check Windows Event Viewer for detailed error messages
- Verify the target architecture matches your system (x64, x86, or ARM64)

## Additional Options

### Optimize for Size
Add these properties to reduce size (may impact compatibility):
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:EnableCompressionInSingleFile=true
```

### Include Debug Symbols
For troubleshooting production issues:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=embedded
```


