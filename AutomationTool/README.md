# .NET Automation Tool

A powerful .NET web application built with ASP.NET Core Blazor that provides advanced desktop automation capabilities through image recognition, conditional scripting, and real-time screenshot capture.

## 🎯 Features

### 📸 Real-time Screenshot Capture
- **Direct Screen Capture**: Take screenshots directly within the application
- **Region Selection**: Capture specific screen regions with customizable coordinates
- **Template Creation**: Automatically save captured regions as template images for recognition
- **No Manual Upload**: Seamless workflow without requiring file uploads

### 🧠 Advanced Image Recognition
- **OpenCV Integration**: Powered by EmguCV for robust image recognition
- **Template Matching**: Find UI elements using pre-captured template images
- **Confidence Thresholds**: Configurable matching precision (10% - 100%)
- **Multiple Detection**: Support for finding all instances of an image
- **Real-time Processing**: Fast image matching with performance metrics

### 📜 Conditional Scripting Engine
- **Visual Script Editor**: Intuitive UI for building automation scripts
- **Conditional Logic**: IF/ELSE statements with multiple condition types
- **Flow Control**: Jump between steps, loops, and branches
- **Script Templates**: Pre-built templates for common automation patterns

### ⚡ Supported Actions
- **Mouse Operations**: Click, double-click, right-click at specific coordinates
- **Keyboard Input**: Type text and send key combinations (Ctrl+C, Alt+Tab, etc.)
- **Wait Commands**: Pause execution for specified durations
- **Screenshot Actions**: Capture images during script execution
- **Conditional Branching**: Dynamic script flow based on conditions

### 🧰 Script Execution Engine
- **Background Processing**: Scripts run independently without blocking the UI
- **Real-time Monitoring**: Live execution status and progress tracking
- **Comprehensive Logging**: Detailed logs with timestamps and execution details
- **Process Control**: Start, pause, resume, and stop script execution
- **Multiple Scripts**: Run multiple automation scripts simultaneously

### 💾 Data Management
- **File-based Storage**: JSON-based script and template persistence
- **Import/Export**: Save and load automation scripts
- **Template Library**: Manage reusable image templates
- **Execution History**: Track script performance and results

## 🛠️ Technical Architecture

### Backend Technologies
- **.NET 8**: Latest .NET framework with Windows targeting
- **ASP.NET Core**: High-performance web framework
- **Blazor Server**: Real-time web UI with SignalR

### Image Processing
- **EmguCV 4.9**: OpenCV wrapper for .NET
- **System.Drawing**: Windows Graphics API integration
- **Template Matching**: Advanced computer vision algorithms

### Desktop Automation
- **Windows APIs**: Native mouse and keyboard control
- **User32.dll**: Low-level input simulation
- **Multi-monitor Support**: Cross-screen automation capabilities

### Data Persistence
- **JSON Storage**: Human-readable script definitions
- **File System**: Organized template and script management
- **In-memory Caching**: Fast access to frequently used data

## 🚀 Getting Started

### Prerequisites
- **Windows 10/11**: Required for desktop automation features
- **.NET 8 SDK**: Download from [Microsoft .NET](https://dotnet.microsoft.com/download)
- **Visual Studio 2022** or **VS Code**: Recommended development environment

### Installation

1. **Clone the Repository**
   ```bash
   git clone <repository-url>
   cd AutomationTool
   ```

2. **Restore Dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the Application**
   ```bash
   dotnet build
   ```

4. **Run the Application**
   ```bash
   dotnet run
   ```

5. **Access the Web Interface**
   - Open your browser to `https://localhost:5001`
   - Navigate through the different sections using the sidebar

### First Steps

1. **Take a Screenshot**
   - Go to the "Screenshot" page
   - Choose full screen or select a region
   - Save useful UI elements as templates

2. **Create Your First Script**
   - Navigate to "Scripts" page
   - Click "New Script" or use a template
   - Add steps with actions and conditions

3. **Execute and Monitor**
   - Go to "Execution" page
   - Start your script and monitor progress
   - View real-time logs and execution status

## 📖 Usage Guide

### Creating Template Images

1. **Navigate to Screenshot Page**
   - Select capture mode (Full Screen or Region)
   - For regions, set X, Y, Width, Height coordinates
   - Enter a descriptive template name
   - Click "Capture Screenshot"
   - Click "Save as Template"

2. **Template Management**
   - View all templates in the "Templates" page
   - Edit template names and match thresholds
   - Test templates against current screen
   - Delete unused templates

### Building Automation Scripts

1. **Script Structure**
   ```json
   {
     "name": "Sample Script",
     "description": "Example automation",
     "steps": [
       {
         "type": "condition",
         "conditions": [
           {
             "type": "image_found",
             "parameters": {
               "templateImageId": "button-template-id",
               "threshold": 0.8
             }
           }
         ],
         "actions": [
           {
             "type": "click",
             "parameters": {
               "x": 500,
               "y": 300
             }
           }
         ]
       }
     ]
   }
   ```

2. **Step Types**
   - **Condition**: Evaluate image presence, timeouts, etc.
   - **Action**: Perform mouse/keyboard operations
   - **Wait**: Pause execution
   - **Jump**: Navigate to different steps

3. **Action Types**
   - `click`: Mouse click at coordinates
   - `double_click`: Double-click action
   - `right_click`: Right-click context menu
   - `type`: Text input simulation
   - `key_press`: Keyboard shortcuts
   - `wait`: Pause execution
   - `screenshot`: Capture current screen

### Script Execution

1. **Starting Scripts**
   - Select script from the Scripts page
   - Click "Run" to start execution
   - Monitor progress in Execution page

2. **Execution Controls**
   - **Pause**: Temporarily halt execution
   - **Resume**: Continue paused scripts
   - **Stop**: Terminate script execution

3. **Monitoring and Logs**
   - Real-time execution status
   - Step-by-step progress tracking
   - Detailed logging with timestamps
   - Error reporting and debugging

## 🏗️ Project Structure

```
AutomationTool/
├── Components/
│   ├── Layout/                 # Application layout components
│   └── Pages/                  # Blazor pages
│       ├── Home.razor         # Dashboard and overview
│       ├── Screenshot.razor   # Screen capture interface
│       ├── Scripts.razor      # Script management
│       ├── Templates.razor    # Template management
│       └── Execution.razor    # Execution monitoring
├── Services/                   # Core business logic
│   ├── ScreenshotService.cs   # Screen capture functionality
│   ├── ImageRecognitionService.cs # Computer vision
│   ├── AutomationEngine.cs    # Mouse/keyboard automation
│   ├── ScriptExecutionService.cs # Script runner
│   └── ScriptStorageService.cs # Data persistence
├── Models/                     # Data models and DTOs
│   └── ScriptModels.cs        # Script and template definitions
├── Data/                      # Application data storage
│   ├── Scripts/               # Saved automation scripts
│   └── Templates/             # Template images and metadata
└── wwwroot/                   # Static web assets
```

## 🔧 Configuration

### Application Settings

Edit `appsettings.json` to configure:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Service Registration

Services are registered in `Program.cs`:

```csharp
// Core automation services
builder.Services.AddSingleton<IScreenshotService, ScreenshotService>();
builder.Services.AddSingleton<IImageRecognitionService, ImageRecognitionService>();
builder.Services.AddSingleton<IAutomationEngine, WindowsAutomationEngine>();
builder.Services.AddSingleton<IScriptStorageService, FileBasedScriptStorageService>();
builder.Services.AddSingleton<IScriptExecutionService, ScriptExecutionService>();
```

## 🚨 Important Notes

### Security Considerations
- **Windows APIs**: Requires elevated permissions for some automation features
- **Network Access**: Web interface accessible via localhost by default
- **File System**: Scripts and templates stored in application directory

### Limitations
- **Windows Only**: Desktop automation limited to Windows platform
- **Browser Dependencies**: Requires modern web browser for UI
- **Performance**: Image recognition speed depends on template complexity

### Best Practices
- **Template Quality**: Use clear, unique UI elements for templates
- **Error Handling**: Include timeout conditions and error recovery
- **Testing**: Test scripts thoroughly before production use
- **Documentation**: Document complex automation workflows

## 🛠️ Development

### Adding New Features

1. **Service Layer**: Implement business logic in Services folder
2. **Models**: Define data structures in Models folder
3. **UI Components**: Create Blazor components for user interaction
4. **Service Registration**: Register new services in Program.cs

### Debugging

1. **Logging**: Check console output for detailed logs
2. **Breakpoints**: Use Visual Studio debugger
3. **Browser Tools**: Inspect Blazor components in browser
4. **Execution Logs**: Monitor script execution in real-time

### Testing

```bash
# Run unit tests
dotnet test

# Run with specific configuration
dotnet run --configuration Release
```

## 📝 License

This project is provided as-is for educational and development purposes. Please ensure compliance with your organization's automation policies and relevant terms of service for automated applications.

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## 📞 Support

For issues, questions, or feature requests:

1. Check the execution logs for error details
2. Review the troubleshooting section
3. Create an issue in the repository

---

**Built with ❤️ using .NET 8, Blazor, and OpenCV**