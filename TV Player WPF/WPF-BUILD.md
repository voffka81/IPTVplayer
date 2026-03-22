# WPF Build Instructions

## Overview
This is a Windows TV Player application built with .NET WPF (Windows Presentation Foundation) targeting net8.0-windows.

## Prerequisites

### System Requirements
- Windows 10 Build 19041 or later (or Windows 11)
- Visual Studio 2022 17.0 or later with:
  - .NET desktop development workload
  - Windows Forms development tools
  - XAML tools

### Development Setup
- .NET 8 SDK or later
- Visual Studio 2022 or JetBrains Rider

## Installation

### Clone and Open Project
```bash
cd "TV Player WPF"
```

### Restore Dependencies
```bash
dotnet restore
```

## Building

### Build for Release
```bash
dotnet build --configuration Release --framework net8.0-windows10.0.19041.0
```

### Build for Debug
```bash
dotnet build --configuration Debug --framework net8.0-windows10.0.19041.0
```

### Build for Distribution
```bash
dotnet publish --configuration Release --framework net8.0-windows10.0.19041.0 -p:PublishProfile=FolderProfile
```

## Running

### Run from Visual Studio
1. Open `TV Player WPF.csproj` in Visual Studio
2. Press F5 to start with debugging
3. Press Ctrl+F5 to start without debugging

### Run from Command Line
```bash
dotnet run --configuration Debug --framework net8.0-windows10.0.19041.0
```

### Run Published Application
```bash
# After publishing
./bin/Release/net8.0-windows10.0.19041.0/publish/TV Player WPF.exe
```

## Project Structure

```
TV Player WPF/
├── ViewModels/           # MVVM ViewModels with INotifyPropertyChanged
│   ├── MainViewModel.cs  # Main window logic
│   ├── PlayerViewModel.cs
│   ├── SettingsViewModel.cs
│   └── ...
├── PlaylistWorker/       # M3U and EPG parsing
│   ├── M3UParser.cs
│   └── M3UInfo.cs
├── MainWindow.xaml       # Main UI
├── VideoPlayer.xaml      # Video player UI
├── Assets/               # Application resources
│   ├── AppStyle.xaml
│   └── Images/
└── TV Player WPF.csproj  # Project file
```

## Configuration

### Playlist Settings
The application loads playlists from configured URLs:
- **Default M3U URL**: Configured in TVPlayerViewModel
- **EPG URL**: Extracted from M3U file or set in settings

To change the playlist:
1. Go to Settings in the application
2. Enter the M3U playlist URL
3. Save settings

Settings are persisted in AppData.

### Application Settings
User preferences are stored in:
```
%localappdata%\TV_Player\settings.json
```

## Key Features

### MVVM Architecture
- Proper separation of concerns with ViewModels
- Data binding through INotifyPropertyChanged
- Commands for user interactions

### Error Handling
- Comprehensive exception handling with logging
- User-friendly error messages
- Debug output for troubleshooting

### Resource Management
- Proper disposal of resources
- No memory leaks from subscriptions
- Clean shutdown sequence

### Performance
- Asynchronous network operations
- Lazy-loaded playlist data
- Caching of EPG information

## Keyboard Shortcuts

- **Escape**: Exit application
- **Backspace**: Go back/navigate
- **F11**: Toggle fullscreen
- **Arrow Keys**: Navigate UI

## Registry Settings

The application may create registry entries in:
```
HKEY_CURRENT_USER\Software\TV_Player
```

These include:
- Last played channel
- Window state and position
- User preferences

## Troubleshooting

### Application Won't Start
1. Check .NET 8 is installed: `dotnet --version`
2. Verify Windows version: `winver` (must be 19041+)
3. Check event viewer for errors: `eventvwr.msc`

### Playlist Download Fails
1. Check network connectivity
2. Verify URL is correct
3. Check firewall settings allow outbound HTTP/HTTPS
4. Check Debug output for detailed error

### Video Won't Play
1. Verify stream URL is valid
2. Check network connection to stream server
3. Verify media format is supported
4. Check sufficient bandwidth available

### Performance Issues
1. Check Task Manager for CPU/memory usage
2. Disable hardware acceleration in settings if available
3. Clear EPG cache and reload
4. Close unnecessary background applications

## Development

### Debug Logging
When running in Debug configuration, detailed logs are sent to Output window in Visual Studio.

Enable more detailed logging:
1. Open Debug > Windows > Output
2. Select "Debug" from dropdown

### Code Organization
- **ViewModels**: Business logic and state management
- **Views**: XAML UI definitions
- **PlaylistWorker**: Network and parsing operations
- **Assets**: Application resources and styling

### Common Tasks

#### Add New ViewModel
```csharp
public class MyViewModel : ObservableViewModelBase
{
    private string _property;
    public string Property 
    { 
        get => _property;
        set => SetProperty(ref _property, value);
    }
}
```

#### Handle Exceptions
```csharp
try 
{
    await FetchPlaylistAsync();
}
catch (HttpRequestException ex)
{
    Debug.WriteLine($"Network error: {ex.Message}");
    // Show user-friendly error
}
```

## Publishing

### Create Installer
Use Visual Studio Setup Project or:
```bash
dotnet publish --configuration Release \
  --framework net8.0-windows10.0.19041.0 \
  --output ./publish
```

Then package with your installer tool (NSIS, WiX, etc.)

### Self-Contained Deployment
```bash
dotnet publish --configuration Release \
  --framework net8.0-windows10.0.19041.0 \
  --self-contained \
  --output ./publish-standalone
```

This creates an executable that doesn't require .NET runtime installed.

## Performance Optimization

- Use **Release** build for distribution
- Enable **ReadyToRun**: `-p:PublishReadyToRun=true`
- Enable **PublishTrimmed**: `-p:PublishTrimmed=true` (advanced)
- Use **PublishAot** for maximum performance (requires additional testing)

## Version History

See CHANGELOG.md for detailed version information.
