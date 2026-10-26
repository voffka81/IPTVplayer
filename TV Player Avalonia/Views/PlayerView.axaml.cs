using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TV_Player.AvaloniaApp.ViewModels;

namespace TV_Player.AvaloniaApp.Views;

public partial class PlayerView : UserControl
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer _overlayTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _overlayVisible = true;
    private bool _hasPlaybackStarted;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private PlayerViewModel? _viewModel;
    private bool _initialized;
    private CancellationTokenSource? _bufferingWatchdogCts;
    private string? _activeStreamUrl;
    private IReadOnlyList<string> _fallbackStreamUrls = Array.Empty<string>();
    private int _fallbackStreamIndex;
    private bool _fallbackAttempted;

    public PlayerView()
    {
        InitializeComponent();
        PlayerClock.Text = DateTime.Now.ToString("HH:mm");
        _clockTimer.Tick += (_, _) => PlayerClock.Text = DateTime.Now.ToString("HH:mm");
        SizeChanged += OnPlayerSizeChanged;
        UpdateResponsiveControls(Bounds.Size);
        _overlayTimer.Tick += OnOverlayTimerTick;
        Log("PlayerView ctor");
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnPlayerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveControls(e.NewSize);
    }

    private void UpdateResponsiveControls(Size size)
    {
        // Avalonia layout units already account for OS DPI scaling.
        // Scale the overlay with the available video viewport rather than fixed 48px buttons.
        if (size.Width <= 0 || size.Height <= 0)
            return;

        var scale = Math.Clamp(Math.Min(size.Width / 800.0, size.Height / 450.0), 0.85, 1.8);
        var barHeight = Math.Clamp(80 * scale, 68, 140);
        OverlayPanel.RowDefinitions[0].Height = new GridLength(barHeight);
        OverlayPanel.RowDefinitions[2].Height = new GridLength(barHeight);

        var buttonSize = Math.Clamp(54 * scale, 48, 94);
        foreach (var button in new[] { PlayerNav1, PlayerNav2, PlayerNav3, PlayerNav4, PlayerNav5 })
        {
            button.Width = buttonSize;
            button.Height = buttonSize;
            button.Margin = new Thickness(0);
            button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            button.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        }

        var topColumnWidth = Math.Max(buttonSize + 16, 68);
        foreach (var column in PlayerTopBar.ColumnDefinitions)
        {
            if (column.Width.IsAbsolute)
                column.Width = new GridLength(topColumnWidth);
        }

        PlayerBottomBar.ColumnDefinitions[0].Width = new GridLength(Math.Max(buttonSize * 2 + 45 * scale, 155));
        PlayerBottomBar.ColumnDefinitions[2].Width = new GridLength(Math.Max(24, 40 * scale));
        GuidePanel.Width = Math.Min(size.Width * 0.45, 360 * scale);
        GuideProgress.Width = Math.Clamp(size.Width * 0.25, 100, 360);
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_initialized)
            return;

        _clockTimer.Start();
        ShowOverlay();
        Log("Attach to visual tree -> initializing VLC");
        try
        {
            Log("Core.Initialize() start");
            Core.Initialize();
            Log("Core.Initialize() completed");

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

            var videoView = this.FindControl<LibVLCSharp.Avalonia.VideoView>("VideoView")
                ?? throw new InvalidOperationException("VideoView control was not found in PlayerView.");
            videoView.MediaPlayer = _mediaPlayer;
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

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _clockTimer.Stop();
        _overlayTimer.Stop();
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

        // NativeControlHost may destroy its HWND after this UserControl detaches.
        // Clear VideoView.MediaPlayer before disposing the native MediaPlayer;
        // otherwise VideoView.Detach() can call libvlc_media_player_set_hwnd
        // on an already-freed native pointer (0xC0000005).
        var videoView = this.FindControl<LibVLCSharp.Avalonia.VideoView>("VideoView");
        if (videoView != null)
        {
            videoView.MediaPlayer = null;
            Log("VideoView.MediaPlayer detached before disposal");
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
        _hasPlaybackStarted = false;
        _fallbackAttempted = false;
        _initialized = false;
    }

    private void OnOverlayTimerTick(object? sender, System.EventArgs e)
    {
        if (_hasPlaybackStarted)
            HideOverlay();
    }

    private void ShowOverlay()
    {
        _overlayVisible = true;
        OverlayPanel.IsVisible = true;
        _overlayTimer.Stop();
        if (_hasPlaybackStarted)
            _overlayTimer.Start();
    }

    private void HideOverlay()
    {
        if (!_hasPlaybackStarted)
        {
            ShowOverlay();
            return;
        }

        _overlayTimer.Stop();
        _overlayVisible = false;
        OverlayPanel.IsVisible = false;
        if (_viewModel != null)
            _viewModel.ProgramGuideVisible = false;
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_hasPlaybackStarted)
        {
            ShowOverlay();
            return;
        }

        // Buttons and other interactive elements retain their own commands.
        // A click on the video/background toggles the control overlay.
        if (e.Source is Avalonia.Visual source)
        {
            for (Avalonia.Visual? current = source; current != null && current != OverlayPanel; current = current.GetVisualParent())
            {
                if (current is Button || current is ScrollViewer)
                {
                    ShowOverlay();
                    return;
                }
            }
        }

        if (_overlayVisible)
            HideOverlay();
        else
            ShowOverlay();
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
            return;
        }

        try
        {
            var streamUrl = _viewModel.StreamUrl.Trim();
            Log($"PlayCurrentStream start. Url={FormatUrlForLog(streamUrl)}");
            _hasPlaybackStarted = false;
            ShowOverlay();
            _activeStreamUrl = streamUrl;
            _fallbackStreamUrls = _viewModel.FallbackStreamUrls;
            _fallbackStreamIndex = 0;
            _fallbackAttempted = false;

            _mediaPlayer.Stop();
            _currentMedia?.Dispose();
            _currentMedia = null;

            _viewModel.SetPlaybackStatus(string.Empty);
            StartBufferingWatchdog();

            var started = StartPlayback(streamUrl, fallback: false);
            Log($"Primary playback start result: {started}");
            if (!started && !TryStartNextStreamFallback())
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

        Log($"StartPlayback called. fallback={fallback}, url={FormatUrlForLog(streamUrl)}");

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

            Dispatcher.UIThread.Post(() =>
            {
                if (token.IsCancellationRequested || _mediaPlayer == null || _viewModel == null ||
                    string.IsNullOrWhiteSpace(_activeStreamUrl) || _mediaPlayer.IsPlaying)
                    return;

                if (TryStartNextStreamFallback())
                    return;

                if (_fallbackAttempted)
                    return;

                _fallbackAttempted = true;
                Log("Buffering watchdog triggered fallback attempt");
                _viewModel.SetPlaybackStatus(string.Empty);
                var started = StartPlayback(_activeStreamUrl!, fallback: true);
                Log($"Fallback playback start result: {started}");
                if (!started)
                {
                    _viewModel.SetPlaybackStatus("Fallback playback failed. Try Open Externally for this channel.");
                }
            });
        }, token);
    }

    private bool TryStartNextStreamFallback()
    {
        if (_mediaPlayer == null || _viewModel == null || _fallbackStreamIndex >= _fallbackStreamUrls.Count)
            return false;

        var fallbackUrl = _fallbackStreamUrls[_fallbackStreamIndex++];
        _activeStreamUrl = fallbackUrl;
        _hasPlaybackStarted = false;
        ShowOverlay();
        _fallbackAttempted = false;
        Log($"Trying alternate stream URL {_fallbackStreamIndex}/{_fallbackStreamUrls.Count}: {FormatUrlForLog(fallbackUrl)}");

        _mediaPlayer.Stop();
        _currentMedia?.Dispose();
        _currentMedia = null;
        _viewModel.SetPlaybackStatus("Trying alternate stream…");
        StartBufferingWatchdog();

        var started = StartPlayback(fallbackUrl, fallback: false);
        Log($"Alternate stream start result: {started}");
        return started;
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
            // Buffering is transient: keep the video unobstructed.
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
            _hasPlaybackStarted = true;
            ShowOverlay();
            _viewModel.SetPlaybackStatus(string.Empty);
        });
    }

    private void OnMediaPlayerEncounteredError(object? sender, System.EventArgs e)
    {
        if (_viewModel == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            Log("MediaPlayer event: EncounteredError");
            if (TryStartNextStreamFallback())
                return;

            _bufferingWatchdogCts?.Cancel();
            _hasPlaybackStarted = false;
            ShowOverlay();
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
            _hasPlaybackStarted = false;
            ShowOverlay();
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
            // Stop is expected while switching channels; avoid a persistent toast.
        });
    }

    private static string FormatUrlForLog(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "<empty>";

        return url.Trim();
    }

    private static void Log(string message)
    {
        var line = $"[PlayerView] {DateTime.Now:HH:mm:ss.fff} {message}";
        Debug.WriteLine(line);
        Console.WriteLine(line);

        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TVPlayer", "Logs", "player.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd} {line}{Environment.NewLine}");
        }
        catch
        {
            // Keep player diagnostics from interrupting playback when the log is unavailable.
        }
    }
}
