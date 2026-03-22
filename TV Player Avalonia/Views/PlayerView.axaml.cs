using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TV_Player.AvaloniaApp.ViewModels;

namespace TV_Player.AvaloniaApp.Views;

public partial class PlayerView : UserControl
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private PlayerViewModel? _viewModel;
    private bool _initialized;
    private CancellationTokenSource? _bufferingWatchdogCts;
    private string? _activeStreamUrl;
    private bool _fallbackAttempted;

    public PlayerView()
    {
        AvaloniaXamlLoader.Load(this);
        Log("PlayerView ctor");
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_initialized)
            return;

        Log("Attach to visual tree -> initializing VLC");
        try
        {
            string? macPluginDirectory = null;
            if (OperatingSystem.IsMacOS())
            {
                if (!TryGetMacLibVlcPaths(out var libVlcDirectory, out macPluginDirectory))
                {
                    const string message = "VLC native dependencies not found. Build once with internet to auto-bundle natives/macos (lib + plugins).";
                    Log(message);
                    _viewModel?.SetPlaybackStatus(message);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(macPluginDirectory) && Directory.Exists(macPluginDirectory))
                {
                    Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", macPluginDirectory);
                    Log($"VLC_PLUGIN_PATH set to: {macPluginDirectory}");
                }

                var dyldLibraryPath = PrependPath(Environment.GetEnvironmentVariable("DYLD_LIBRARY_PATH"), libVlcDirectory);
                var dyldFallbackPath = PrependPath(Environment.GetEnvironmentVariable("DYLD_FALLBACK_LIBRARY_PATH"), libVlcDirectory);
                Environment.SetEnvironmentVariable("DYLD_LIBRARY_PATH", dyldLibraryPath);
                Environment.SetEnvironmentVariable("DYLD_FALLBACK_LIBRARY_PATH", dyldFallbackPath);
                Log($"DYLD_LIBRARY_PATH set to: {dyldLibraryPath}");
                Log($"DYLD_FALLBACK_LIBRARY_PATH set to: {dyldFallbackPath}");

                Log($"Core.Initialize(path) start: {libVlcDirectory}");
                Core.Initialize(libVlcDirectory);
                Log("Core.Initialize(path) completed");
            }
            else
            {
                Log("Core.Initialize() start");
                Core.Initialize();
                Log("Core.Initialize() completed");
            }

            _libVlc = new LibVLC(enableDebugLogs: true);
            Log("LibVLC instance created");

            _mediaPlayer = new MediaPlayer(_libVlc)
            {
                // Software decode is more reliable for mixed IPTV codecs on macOS.
                EnableHardwareDecoding = !OperatingSystem.IsMacOS()
            };
            Log($"MediaPlayer created. HW decoding enabled: {_mediaPlayer.EnableHardwareDecoding}");

            _mediaPlayer.Buffering += OnMediaPlayerBuffering;
            _mediaPlayer.Playing += OnMediaPlayerPlaying;
            _mediaPlayer.EncounteredError += OnMediaPlayerEncounteredError;
            _mediaPlayer.EndReached += OnMediaPlayerEndReached;
            _mediaPlayer.Stopped += OnMediaPlayerStopped;

            VideoView.MediaPlayer = _mediaPlayer;
            _initialized = true;
            Log("VideoView.MediaPlayer assigned");
            PlayCurrentStream();
        }
        catch (System.Exception ex)
        {
            Log($"VLC initialization failed: {ex}");
            _viewModel?.SetPlaybackStatus($"VLC init failed: {ex.Message}");
        }
    }

    private static bool TryGetMacLibVlcPaths(out string libDirectory, out string pluginDirectory)
    {
        var outputBase = AppContext.BaseDirectory;
        var candidates = new[]
        {
            outputBase,
            Path.Combine(outputBase, "natives", "macos", "lib"),
            Path.Combine(outputBase, "lib"),
            Path.Combine(outputBase, "libvlc", "osx-x64", "lib"),
            "/Applications/VLC.app/Contents/MacOS/lib",
            "/opt/homebrew/lib",
            "/usr/local/lib"
        };

        foreach (var candidateDir in candidates)
        {
            var libvlc = Path.Combine(candidateDir, "libvlc.dylib");
            var libvlccore = Path.Combine(candidateDir, "libvlccore.dylib");
            if (File.Exists(libvlc) && File.Exists(libvlccore))
            {
                var pluginCandidates = new[]
                {
                    Path.Combine(candidateDir, "plugins"),
                    Path.Combine(Path.GetDirectoryName(candidateDir) ?? string.Empty, "plugins")
                };

                pluginDirectory = string.Empty;
                foreach (var candidatePluginDir in pluginCandidates)
                {
                    if (Directory.Exists(candidatePluginDir))
                    {
                        pluginDirectory = candidatePluginDir;
                        break;
                    }
                }

                libDirectory = candidateDir;
                Log($"Found macOS VLC libs in: {candidateDir}");
                if (!string.IsNullOrWhiteSpace(pluginDirectory))
                {
                    Log($"Found macOS VLC plugins in: {pluginDirectory}");
                }
                else
                {
                    Log("macOS VLC plugins directory not found next to libraries");
                }
                return true;
            }
        }

        libDirectory = string.Empty;
        pluginDirectory = string.Empty;
        Log("macOS VLC libs not found. Checked directories: " + string.Join("; ", candidates));
        return false;
    }

    private static string PrependPath(string? existingPaths, string pathToPrepend)
    {
        if (string.IsNullOrWhiteSpace(pathToPrepend))
            return existingPaths ?? string.Empty;

        if (string.IsNullOrWhiteSpace(existingPaths))
            return pathToPrepend;

        var parts = existingPaths.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Contains(pathToPrepend))
            return existingPaths;

        return pathToPrepend + ":" + existingPaths;
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        Log("Detach from visual tree -> disposing player resources");
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (_mediaPlayer != null)
        {
            _mediaPlayer.Buffering -= OnMediaPlayerBuffering;
            _mediaPlayer.Playing -= OnMediaPlayerPlaying;
            _mediaPlayer.EncounteredError -= OnMediaPlayerEncounteredError;
            _mediaPlayer.EndReached -= OnMediaPlayerEndReached;
            _mediaPlayer.Stopped -= OnMediaPlayerStopped;
        }

        _mediaPlayer?.Stop();
        _bufferingWatchdogCts?.Cancel();
        _bufferingWatchdogCts?.Dispose();
        _currentMedia?.Dispose();
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();
        _bufferingWatchdogCts = null;
        _currentMedia = null;
        _mediaPlayer = null;
        _libVlc = null;
        _activeStreamUrl = null;
        _fallbackAttempted = false;
        _initialized = false;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        Log($"DataContext changed. New type: {DataContext?.GetType().Name ?? "null"}");
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as PlayerViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        PlayCurrentStream();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.StreamUrl))
        {
            Log("ViewModel StreamUrl changed -> replaying stream");
            PlayCurrentStream();
        }
    }

    private void PlayCurrentStream()
    {
        if (!_initialized || _viewModel == null || _mediaPlayer == null || _libVlc == null)
        {
            Log($"PlayCurrentStream skipped: initialized={_initialized}, hasVm={_viewModel != null}, hasPlayer={_mediaPlayer != null}, hasLibVlc={_libVlc != null}");
            return;
        }

        if (string.IsNullOrWhiteSpace(_viewModel.StreamUrl))
        {
            Log("PlayCurrentStream aborted: empty StreamUrl");
            _viewModel.SetPlaybackStatus("No stream URL available for this channel.");
            return;
        }

        try
        {
            var streamUrl = _viewModel.StreamUrl.Trim();
            Log($"PlayCurrentStream start. Url={SanitizeUrlForLog(streamUrl)}");
            _activeStreamUrl = streamUrl;
            _fallbackAttempted = false;

            _mediaPlayer.Stop();
            _currentMedia?.Dispose();
            _currentMedia = null;

            _viewModel.SetPlaybackStatus("Buffering stream...");
            StartBufferingWatchdog();

            var started = StartPlayback(streamUrl, fallback: false);
            Log($"Primary playback start result: {started}");
            if (!started)
            {
                _viewModel.SetPlaybackStatus("Stream failed to start. Try another channel or Open Externally.");
            }
        }
        catch (System.Exception ex)
        {
            Log($"PlayCurrentStream exception: {ex}");
            _viewModel.SetPlaybackStatus($"Embedded playback failed: {ex.Message}. Use Open Externally as a fallback.");
        }
    }

    private static Media BuildMediaFromStreamUrl(LibVLC libVlc, string rawStreamUrl)
    {
        // Common IPTV format: http://.../stream.m3u8|user-agent=...&referer=...
        var splitIndex = rawStreamUrl.IndexOf('|');
        if (splitIndex <= 0)
        {
            return new Media(libVlc, rawStreamUrl, FromType.FromLocation);
        }

        var baseUrl = rawStreamUrl[..splitIndex].Trim();
        var optionsSegment = rawStreamUrl[(splitIndex + 1)..].Trim();
        var media = new Media(libVlc, baseUrl, FromType.FromLocation);

        if (string.IsNullOrWhiteSpace(optionsSegment))
        {
            return media;
        }

        var pairs = optionsSegment.Split('&', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var equalsIndex = pair.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex == pair.Length - 1)
            {
                continue;
            }

            var key = pair[..equalsIndex].Trim().ToLowerInvariant();
            var value = HttpUtility.UrlDecode(pair[(equalsIndex + 1)..].Trim());
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (key)
            {
                case "user-agent":
                case "http-user-agent":
                    media.AddOption($":http-user-agent={value}");
                    break;
                case "referer":
                case "referrer":
                case "http-referrer":
                case "http-referer":
                    media.AddOption($":http-referrer={value}");
                    break;
                case "origin":
                    media.AddOption($":http-header=Origin={value}");
                    break;
                case "cookie":
                case "cookies":
                    media.AddOption($":http-header=Cookie={value}");
                    break;
            }
        }

        return media;
    }

    private bool StartPlayback(string streamUrl, bool fallback)
    {
        if (_mediaPlayer == null || _libVlc == null)
            return false;

        Log($"StartPlayback called. fallback={fallback}, url={SanitizeUrlForLog(streamUrl)}");

        _currentMedia?.Dispose();
        _currentMedia = BuildMediaFromStreamUrl(_libVlc, streamUrl);
        _currentMedia.AddOption(":network-caching=1500");
        _currentMedia.AddOption(":live-caching=1500");
        _currentMedia.AddOption(":http-reconnect=true");
        _currentMedia.AddOption(":codec=avcodec");
        if (OperatingSystem.IsMacOS())
        {
            _currentMedia.AddOption(":avcodec-hw=none");
        }

        if (fallback)
        {
            _currentMedia.AddOption(":demux=any");
            _currentMedia.AddOption(":hls-segment-threads=4");
            _currentMedia.AddOption(":http-continuous=true");
            Log("Fallback options applied: demux=any, hls-segment-threads=4, http-continuous=true");
        }

        var started = _mediaPlayer.Play(_currentMedia);
        Log($"MediaPlayer.Play returned: {started}");
        return started;
    }

    private void StartBufferingWatchdog()
    {
        _bufferingWatchdogCts?.Cancel();
        _bufferingWatchdogCts?.Dispose();
        _bufferingWatchdogCts = new CancellationTokenSource();
        var token = _bufferingWatchdogCts.Token;
        Log("Buffering watchdog started (12s)");

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(12), token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested)
                return;

            if (_mediaPlayer == null || _viewModel == null || string.IsNullOrWhiteSpace(_activeStreamUrl))
                return;

            if (_mediaPlayer.IsPlaying || _fallbackAttempted)
                return;

            _fallbackAttempted = true;
            Log("Buffering watchdog triggered fallback attempt");
            Dispatcher.UIThread.Post(() =>
            {
                if (_viewModel == null)
                    return;

                _viewModel.SetPlaybackStatus("Still buffering, retrying with fallback settings...");
                var started = StartPlayback(_activeStreamUrl!, fallback: true);
                Log($"Fallback playback start result: {started}");
                if (!started)
                {
                    _viewModel.SetPlaybackStatus("Fallback playback failed. Try Open Externally for this channel.");
                }
            });
        }, token);
    }

    private void OnMediaPlayerBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        if (_viewModel == null)
            return;

        if (e.Cache < 1 || ((int)e.Cache % 10) == 0)
        {
            Log($"MediaPlayer buffering: {e.Cache:0}%");
        }

        Dispatcher.UIThread.Post(() =>
        {
            _viewModel.SetPlaybackStatus($"Buffering stream... {e.Cache:0}%");
        });
    }

    private void OnMediaPlayerPlaying(object? sender, System.EventArgs e)
    {
        if (_viewModel == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            _bufferingWatchdogCts?.Cancel();
            Log("MediaPlayer event: Playing");
            _viewModel.SetPlaybackStatus(string.Empty);
        });
    }

    private void OnMediaPlayerEncounteredError(object? sender, System.EventArgs e)
    {
        if (_viewModel == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            _bufferingWatchdogCts?.Cancel();
            Log("MediaPlayer event: EncounteredError");
            _viewModel.SetPlaybackStatus("Playback error. The stream may be unavailable or unsupported.");
        });
    }

    private void OnMediaPlayerEndReached(object? sender, System.EventArgs e)
    {
        if (_viewModel == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            _bufferingWatchdogCts?.Cancel();
            Log("MediaPlayer event: EndReached");
            _viewModel.SetPlaybackStatus("Stream ended.");
        });
    }

    private void OnMediaPlayerStopped(object? sender, System.EventArgs e)
    {
        if (_viewModel == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            _bufferingWatchdogCts?.Cancel();
            Log("MediaPlayer event: Stopped");
            if (string.IsNullOrWhiteSpace(_viewModel.PlaybackStatus))
            {
                _viewModel.SetPlaybackStatus("Playback stopped.");
            }
        });
    }

    private static string SanitizeUrlForLog(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "<empty>";

        var trimmed = url.Trim();
        var tokenIndex = trimmed.IndexOf("token=", System.StringComparison.OrdinalIgnoreCase);
        if (tokenIndex >= 0)
        {
            return "<url-with-token-redacted>";
        }

        var splitIndex = trimmed.IndexOf('|');
        return splitIndex > 0 ? trimmed[..splitIndex] + "|<headers>" : trimmed;
    }

    private static void Log(string message)
    {
        var line = $"[PlayerView] {DateTime.Now:HH:mm:ss.fff} {message}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }
}
