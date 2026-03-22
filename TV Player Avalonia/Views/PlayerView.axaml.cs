using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LibVLCSharp.Shared;
using System.ComponentModel;
using TV_Player.AvaloniaApp.ViewModels;

namespace TV_Player.AvaloniaApp.Views;

public partial class PlayerView : UserControl
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private PlayerViewModel? _viewModel;
    private bool _initialized;

    public PlayerView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_initialized)
            return;

        Core.Initialize();
        _libVlc = new LibVLC(enableDebugLogs: true);
        _mediaPlayer = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = true
        };

        VideoView.MediaPlayer = _mediaPlayer;
        _initialized = true;
        PlayCurrentStream();
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();
        _mediaPlayer = null;
        _libVlc = null;
        _initialized = false;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
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
            PlayCurrentStream();
        }
    }

    private void PlayCurrentStream()
    {
        if (!_initialized || _viewModel == null || _mediaPlayer == null || _libVlc == null)
            return;

        if (string.IsNullOrWhiteSpace(_viewModel.StreamUrl))
        {
            _viewModel.SetPlaybackStatus("No stream URL available for this channel.");
            return;
        }

        try
        {
            if (_mediaPlayer.Media != null)
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Media.Dispose();
            }

            var media = new Media(_libVlc, new Uri(_viewModel.StreamUrl));
            _mediaPlayer.Media = media;
            _mediaPlayer.Play();
            _viewModel.SetPlaybackStatus("Playing with embedded VLC.");
        }
        catch (System.Exception ex)
        {
            _viewModel.SetPlaybackStatus($"Embedded playback failed: {ex.Message}. Use Open Externally as a fallback.");
        }
    }
}
