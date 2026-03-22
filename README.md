# TV Player - Multi-Platform IPTV Application

A comprehensive .NET application for streaming IPTV content across multiple platforms using the MAUI cross-platform framework and WPF for Windows.

## Project Overview

This project provides two implementations:
- **WPF (Windows)** - Primary Windows desktop application
- **MAUI (Multi-platform)** - Cross-platform support for Android, Windows, and macOS

Both implementations share common business logic for M3U playlist parsing and EPG (Electronic Program Guide) handling.

## Architecture

### Shared Components

#### ViewModels (MVVM Pattern)
- **ObservableViewModelBase**: Base class implementing INotifyPropertyChanged
- **TVPlayerViewModel**: Central application state (Lazy<T> singleton)
- **MainViewModel**: Main UI logic
- **PlayerViewModel**: Video playback management
- **ProgramViewModel**: Program/channel selection
- **SettingsViewModel**: User preferences

#### Services & Utilities
- **M3UParser**: Parses M3U playlists using regex
- **M3UInfo**: Represents individual M3U playlist entry
- **ProgramsData**: Manages playlist and EPG data via Reactive Extensions
- **PlaylistSettings**: Configurable settings for URLs and cache behavior

#### Data Models
- **GroupInfo**: Channel grouping information
- **ProgramGuide**: EPG channel data
- **ProgramInfo**: Individual program schedule entry

### Platform-Specific Code

#### WPF Implementation
```
TV Player WPF/
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── PlayerViewModel.cs
│   ├── SettingsViewModel.cs
│   └── ...
├── PlaylistWorker/          # Shared parsing logic
├── MainWindow.xaml
├── VideoPlayer.xaml
└── Assets/                  # Styles and resources
```

**Key Features:**
- Full Windows desktop experience with keyboard shortcuts
- Fullscreen support
- Persistent settings (Windows Registry/AppData)
- Window state management
- Responsive UI with XAML styling

#### MAUI Implementation
```
TV Player/
├── ViewModels/              # Shared with WPF
├── Platforms/
│   ├── Android/
│   ├── Windows/
│   └── MacCatalyst/         # NEW: macOS support
├── MainPage.xaml
├── PlayerPage.xaml
├── AppShell.xaml
└── MauiProgram.cs          # DI configuration
```

**Key Features:**
- Cross-platform support (Android, Windows, macOS)
- Native platform integration
- Responsive UI adapting to screen size
- Singleton pattern with proper initialization
- Async-first design

## Code Quality Improvements

### Critical Fixes Applied

#### 1. Exception Handling
**Before:**
```csharp
catch {} // Silent failure - impossible to debug!
```

**After:**
```csharp
catch (HttpRequestException ex)
{
    System.Diagnostics.Debug.WriteLine($"Network error: {ex.Message}");
}
catch (XmlException ex)
{
    System.Diagnostics.Debug.WriteLine($"XML parse error: {ex.Message}");
}
```

#### 2. Thread-Safe Singleton Pattern
**Before:**
```csharp
if (_instance == null)
    _instance = new TVPlayerViewModel();  // Race condition!
return _instance;
```

**After:**
```csharp
private static readonly Lazy<TVPlayerViewModel> _instance = 
    new Lazy<TVPlayerViewModel>(
        () => new TVPlayerViewModel(), 
        LazyThreadSafetyMode.ExecutionAndPublication);

public static TVPlayerViewModel Instance => _instance.Value;
```

#### 3. Proper Resource Disposal
**Before:**
```csharp
private IDisposable _groupsSubscriber;
// ... disposed only on navigation, leaking memory otherwise
_groupsSubscriber.Dispose();
```

**After:**
```csharp
private CompositeDisposable _disposables = new();

protected override void OnAppearing()
{
    base.OnAppearing();
    _disposables.Add(
        TVPlayerViewModel.Instance.PlaylistData.GroupsInformation
            .Subscribe(x => Programs = x)
    );
}

protected override void OnDisappearing()
{
    base.OnDisappearing();
    _disposables.Dispose();  // Complete cleanup
}
```

#### 4. Safe Application Shutdown
**Before:**
```csharp
Environment.Exit(0);  // Force exit without cleanup
```

**After:**
```csharp
if (Application.Current?.MainWindow is Window window)
{
    window.Close();  // Allows cleanup sequence
}
```

### Additional Improvements

- **Input Validation**: Null checks for navigation and URLs
- **Configuration Management**: PlaylistSettings for URL configuration
- **Base Classes**: Proper inheritance for reusable code
- **Async/Await**: Fire-and-forget tasks now properly tracked
- **DateTime Parsing**: CultureInfo.InvariantCulture for reliability
- **Code Cleanup**: Removed 70+ lines of commented code

## Building & Running

### For WPF (Main Windows Application)
```bash
cd "TV Player WPF"
dotnet build --configuration Release
dotnet run
```
See **WPF-BUILD.md** for detailed instructions.

### For MAUI (Cross-Platform)

#### macOS (NEW!)
```bash
cd "TV Player"
dotnet build --framework net8.0-maccatalyst
dotnet run --framework net8.0-maccatalyst
```

#### Android
```bash
dotnet run --framework net8.0-android
```

#### Windows (MAUI)
```bash
dotnet run --framework net8.0-windows10.0.19041.0
```

See **MAUI-BUILD.md** for detailed instructions and prerequisites.

## Features

### M3U Playlist Support
- **Format**: Standard M3U8 with extended info
- **Parsing**: Regex-based extraction of metadata
- **Grouping**: Programs organized by group/category
- **Logo Support**: Channel logos from M3U metadata
- **Error Handling**: Graceful handling of malformed entries

### EPG (Electronic Program Guide)
- **Format**: XMLTV standard
- **Async Loading**: Non-blocking EPG download and parsing
- **Caching**: Local storage to reduce network requests
- **Timezone Support**: Proper DateTime parsing with culture info

### Streaming
- **Format Support**: M3U8, HTTP, RTMP streams
- **Adaptive**: Handles different stream formats
- **Timeout Protection**: Configurable network timeouts
- **Error Recovery**: Retry logic for failed streams

### UI/UX
- **Cross-Platform**: Consistent experience across platforms
- **Responsive**: Adapts to different screen sizes
- **Accessible**: Keyboard navigation support
- **Dark Theme**: Professional appearance (configurable)

## Configuration

### Playlist Settings
```csharp
public class PlaylistSettings
{
    public string M3UUrl { get; set; }
    public string EpgUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public bool CacheEpgLocally { get; set; } = true;
    public int CacheValidityDays { get; set; } = 3;
}
```

Default configuration uses the provided IPTV service, but can be customized via:
1. Application settings UI
2. Configuration files
3. Programmatic configuration in PlaylistSettings

## Platform Support

| Platform | Status | Minimum Version | Notes |
|----------|--------|-----------------|-------|
| Windows (WPF) | ✅ Primary | Windows 10 19041 | Full-featured |
| Windows (MAUI) | ✅ Supported | Windows 10 19041 | Cross-platform |
| Android | ✅ Supported | API 21+ | MAUI UI |
| macOS | ✅ New | macOS 14.2+ | Mac Catalyst |
| iOS | ⚠️ Possible | iOS 14+ | Not tested |

## Dependencies

### Core NuGet Packages
- **System.Reactive**: 6.0.0+ (Rx streams)
- **Microsoft.Maui.Controls**: 8.0.20+ (MAUI UI framework)
- **CommunityToolkit.Mvvm**: Latest (MAUI DI)

### Optional
- LibVLC (for advanced video playback - currently unused)
- Custom HTTP clients support

## Troubleshooting

### macOS Build Issues
```bash
# Install MAUI workload
dotnet workload install maui
dotnet workload install maccatalyst

# Verify
dotnet workload list
```

### Network Errors
- Check Debug output for specific error messages
- Verify firewall allows outbound connections
- Test URL directly in browser
- Check for HTTPS vs HTTP issues

### Performance
- Use Release build for testing
- Check available memory and CPU
- Monitor network bandwidth

## Development Guidelines

### Adding New Features
1. Implement ViewModel inheriting from ObservableViewModelBase
2. Add proper exception handling (not bare catch blocks!)
3. Use CompositeDisposable for subscriptions
4. Test on all platforms before committing

### Error Handling Pattern
```csharp
try 
{
    // Attempt operation
}
catch (SpecificException ex)
{
    Debug.WriteLine($"Specific error: {ex.Message}");
    // Handle specific case
}
catch (Exception ex)
{
    Debug.WriteLine($"Unexpected error: {ex.Message}");
    // Handle general case
}
```

### Resource Management
Always use using statements or proper disposal:
```csharp
using (var client = new HttpClient())
{
    // Use client
} // Automatic disposal
```

For subscriptions:
```csharp
_disposables.Add(observable.Subscribe(...));
// Cleanup in OnDisappearing or Dispose method
```

## Future Enhancements

### High Priority
- [ ] Shared core library to eliminate duplication
- [ ] Unit tests for M3U parsing
- [ ] Integration tests for EPG loading
- [ ] Settings persistence across platforms
- [ ] Search functionality

### Medium Priority
- [ ] Channel bookmarks/favorites
- [ ] Recording functionality
- [ ] Picture-in-picture support
- [ ] Subtitle support
- [ ] Audio track selection

### Low Priority
- [ ] Push notifications for new channels
- [ ] Social sharing
- [ ] Analytics integration
- [ ] In-app purchases for premium features

## Contributing

### Code Standards
- Follow C# naming conventions (PascalCase for public, camelCase for private)
- Use meaningful variable names
- Add XML comments for public APIs
- Keep methods focused and under 50 lines
- Use async/await for I/O operations

### Pull Requests
1. Create feature branch from main
2. Test on all supported platforms
3. Update documentation
4. Submit PR with description of changes
5. Address review feedback

## License

[Your License Here]

## Contact & Support

For issues, questions, or suggestions:
- Create an issue on repository
- Check existing documentation
- Review debug logs for error details

## Changelog

See CHANGELOG.md for detailed version history.

---

**Last Updated**: March 22, 2026
**Project Status**: Stable (MAUI with macOS support)
**Main Implementation**: WPF for Windows
**Cross-Platform**: MAUI for Android, Windows, macOS
