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


**Last Updated**: March 22, 2026
**Project Status**: Stable (MAUI with macOS support)
**Main Implementation**: WPF for Windows
**Cross-Platform**: MAUI for Android, Windows, macOS
