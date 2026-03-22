# macOS Quick Start Guide

## System Requirements

- **macOS Version**: 14.2 (Sonoma) or later
- **Xcode**: 15.0 or later with Command Line Tools
- **.NET SDK**: 8.0 or later
- **Disk Space**: ~2GB for build artifacts

## One-Time Setup

### 1. Install/Update Xcode Command Line Tools
```bash
xcode-select --install
# OR if already installed, update:
sudo xcode-select --switch /Applications/Xcode.app/xcode-select
```

### 2. Install .NET 8
```bash
# Using Homebrew (recommended)
brew install dotnet

# Verify installation
dotnet --version
```

### 3. Install MAUI Workload
```bash
dotnet workload install maui
dotnet workload install maccatalyst
dotnet workload restore
```

### 4. Verify Installation
```bash
dotnet workload list
# Should show: maui and maccatalyst as installed
```

## Building for the First Time

```bash
# Navigate to project directory
cd "TV Player"

# Restore dependencies
dotnet restore

# Build for macOS
dotnet build --configuration Debug --framework net8.0-maccatalyst

# Or build Release for distribution
dotnet build --configuration Release --framework net8.0-maccatalyst
```

## Running the Application

### Option A: Run Directly from Project
```bash
dotnet run --framework net8.0-maccatalyst
```

### Option B: Run from Visual Studio Code
1. Open workspace in VS Code
2. Install C# Dev Kit extension
3. Select net8.0-maccatalyst as target framework
4. Press F5 to run

### Option C: Run from Compiled Binary
```bash
# After building
./bin/Debug/net8.0-maccatalyst/TV\ Player.app/Contents/MacOS/TV\ Player

# Or for Release
./bin/Release/net8.0-maccatalyst/TV\ Player.app/Contents/MacOS/TV\ Player
```

## Common Issues & Solutions

### Issue: "dotnet: command not found"
**Solution:**
```bash
# Add .NET to PATH
export PATH="$PATH:/usr/local/share/dotnet"

# Make permanent by adding to ~/.zshrc (or ~/.bash_profile for older shells)
echo 'export PATH="$PATH:/usr/local/share/dotnet"' >> ~/.zshrc
source ~/.zshrc
```

### Issue: "Workload 'maccatalyst' not found"
**Solution:**
```bash
# Install missing workload
dotnet workload install maccatalyst

# Or repair all workloads
dotnet workload repair

# Also install maui if not present
dotnet workload install maui
```

### Issue: "Cannot open debugger" on Intel Mac
**Solution:** Use Release build instead:
```bash
dotnet run --configuration Release --framework net8.0-maccatalyst
```

### Issue: "Port 5000 already in use"
**Solution:** Kill existing process:
```bash
# Find process using port 5000
lsof -i :5000

# Kill it (replace PID with actual process ID)
kill -9 <PID>
```

### Issue: "Xcode license agreement not accepted"
**Solution:**
```bash
sudo xcode-select --switch /Applications/Xcode.app/xcode-select
sudo xcode-build-settings-install
```

### Issue: Network requests failing (EPG/M3U download)
**Checking entitlements:**
- Verify `Platforms/MacCatalyst/Entitlements.plist` has network permissions
- Check System Preferences > Security & Privacy > Network

**To debug:**
1. Enable full output: `dotnet run -v d`
2. Check Console.app for system logs
3. Verify firewall settings allow the app

## Optimized Installation Script

Create `setup-macos.sh`:
```bash
#!/bin/bash
set -e

echo "🍎 Setting up TV Player for macOS..."

# Check if Xcode Command Line Tools are installed
if ! xcode-select -p &>/dev/null; then
    echo "Installing Xcode Command Line Tools..."
    xcode-select --install
fi

# Check if .NET is installed
if ! command -v dotnet &>/dev/null; then
    echo "Installing .NET 8 via Homebrew..."
    brew install dotnet
fi

# Install MAUI workloads
echo "Installing MAUI workloads..."
dotnet workload install maui
dotnet workload install maccatalyst

echo "✅ Setup complete!"
echo ""
echo "To build: dotnet build --framework net8.0-maccatalyst"
echo "To run:   dotnet run --framework net8.0-maccatalyst"
```

Make executable and run:
```bash
chmod +x setup-macos.sh
./setup-macos.sh
```

## Performance Tips

### Build Optimization
```bash
# Build with specific platform only (faster)
dotnet build -f net8.0-maccatalyst --configuration Release

# Clean build if issues occur
dotnet clean && dotnet restore && dotnet build
```

### Runtime Optimization
- Close unnecessary applications
- Use Release build for performance testing
- Check Activity Monitor for memory usage

## Debugging

### Enable Verbose Output
```bash
dotnet run --framework net8.0-maccatalyst --verbosity diagnostic
```

### View System Logs
```bash
# Real-time logs from app
log stream --predicate 'process == "TV Player"'

# Or use Console.app
/Applications/Utilities/Console.app
```

### Check Application Cache
```bash
# EPG cache location
~/Library/Application\ Support/TVPlayer/

# Clear cache if needed
rm -rf ~/Library/Application\ Support/TVPlayer/
```

## Creating Distribution Build

### Create Signed Application
```bash
# Build
dotnet build --configuration Release --framework net8.0-maccatalyst

# Create .dmg for distribution
# (Requires certificate signing setup)
```

### Without Notarization (for local testing)
```bash
# Build creates .app in bin/Release
# Run directly:
open ./bin/Release/net8.0-maccatalyst/TV\ Player.app
```

## Architecture-Specific Builds

### Apple Silicon Mac (M1, M2, M3, etc.)
```bash
# Native ARM64 build (automatic)
dotnet build --framework net8.0-maccatalyst
```

### Intel Mac
```bash
# x64 build
dotnet build --framework net8.0-maccatalyst -p:Architecture=x64
```

### Universal Binary (Both ARM64 & x64)
This requires advanced configuration - not standard in MAUI.
For now, builds target native architecture automatically.

## Network Security

The app requires these entitlements (automatically configured):
- "Client network connections" - Download playlists and EPG
- "Server network connections" - Stream media
- HTTP access - For IPTV streams

If you get security warnings:
1. Go to System Preference > Security & Privacy
2. Allow "TV Player" in Network settings if prompted
3. Restart application

## Troubleshooting Checklist

- [ ] Xcode Command Line Tools installed: `xcode-select -p`
- [ ] .NET 8+ installed: `dotnet --version`
- [ ] MAUI workload installed: `dotnet workload list`
- [ ] Hardware connection: `system_profiler SPHardwareDataType`
- [ ] Network access: `ping google.com`
- [ ] Disk space available: `df -h`

## Next Steps

After successful setup:
1. Read main **README.md** for project overview
2. Check **MAUI-BUILD.md** for detailed build documentation  
3. Review **WPF-BUILD.md** to understand Windows version
4. Explore ViewModels in `TV Player/ViewModels/`

## Getting Help

1. **macOS Specific**: Check this file and Console.app logs
2. **Build Errors**: Run with `-v d` for diagnostic output
3. **Runtime Errors**: Check Debug output in IDE
4. **MAUI General**: [MAUI Documentation](https://learn.microsoft.com/en-us/dotnet/maui/)

---

**Last Updated**: March 22, 2026
**Status**: Ready for Development & Distribution
