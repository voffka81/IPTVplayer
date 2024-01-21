using LibVLCSharp.Shared;

namespace TV_Player.MAUI;

public partial class PlayerPage : ContentPage
{
    public static BindableProperty StreamUrlProperty = BindableProperty.Create(nameof(StreamUrl)
         , typeof(string)
         , typeof(MediaViewer)
         , ""
         , defaultBindingMode: BindingMode.TwoWay);

    public string StreamUrl
    {
        get => (string)GetValue(StreamUrlProperty);
        set
        {
            SetValue(StreamUrlProperty, value);
        }
    }

    LibVLC _libVLC;
    MediaPlayer _mediaPlayer;

    
    public VideoPlayer()
    {
        InitializeComponent();

        _libVLC = new LibVLC(enableDebugLogs: true);
        _mediaPlayer = new MediaPlayer(_libVLC);

        VideoView.Loaded += (sender, e) =>
        {
            VideoView.MediaPlayer = _mediaPlayer;
            VideoView.MouseLeftButtonDown += VideoView_MouseLeftButtonDown;
            VideoView.MediaPlayer.EnableMouseInput = false;
            VideoView.PreviewMouseLeftButtonDown += VideoView_MouseLeftButtonDown;
            AutoPlay();
        };
        Unloaded += VideoPlayer_Unloaded;

    }

    private void VideoView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ToggleOverlay();
    }

    private void VideoPlayer_Unloaded(object sender, RoutedEventArgs e)
    {
        VideoView.Dispose();
    }


    void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (VideoView.MediaPlayer.IsPlaying)
        {
            VideoView.MediaPlayer.Pause();
        }
    }

    private void AutoPlay()
    {
        if (!VideoView.MediaPlayer.IsPlaying)
        {

            using (var media = new Media(_libVLC, new Uri(SourceUrl)))
                VideoView.MediaPlayer.Play(media);
        }
    }


    private void MyUserControl_MouseDown(object sender, MouseButtonEventArgs e)
    {
        ToggleOverlay();
    }

    private void MyUserControl_TouchDown(object sender, TouchEventArgs e)
    {
        ToggleOverlay();
    }

    private void ToggleOverlay()
    {
        if (overlayPanel.Visibility == Visibility.Visible)
        {
            HideOverlay();
        }
        else
        {
            ShowOverlay();
        }
    }

    public void ShowOverlay()
    {
        overlayPanel.Visibility = Visibility.Visible;
    }

    public void HideOverlay()
    {
        overlayPanel.Visibility = Visibility.Collapsed;
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        VideoView.MediaPlayer?.Dispose();
    }
}