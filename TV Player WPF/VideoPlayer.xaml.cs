using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for ProgramsGroupGrid.xaml
    /// </summary>
    public partial class VideoPlayer : UserControl
    {
        public static readonly DependencyProperty SourceUrlProperty = DependencyProperty.Register(
            nameof(SourceUrl), typeof(string), typeof(VideoPlayer), new PropertyMetadata(string.Empty, OnSourceUrlChanged));

        public string SourceUrl
        {
            get => (string)GetValue(SourceUrlProperty);
            set => SetValue(SourceUrlProperty, value);
        }

        private static void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VideoPlayer vp && e.NewValue is string url)
            {
                vp.OnSourceUrlChanged(url);
            }
        }

        private void OnSourceUrlChanged(string url)
        {
            // when the bound source changes, trigger playback switch
            try
            {
                VideoView.MediaPlayer?.Stop();
                VideoView.MediaPlayer?.Media?.Dispose();
            }
            catch { }

            AutoPlay();
        }

        private LibVLC _libVLC => LibVLCManager.Instance;
        private MediaPlayer _mediaPlayer;
        private PlayerViewModel _viewModel;
        private VlcHostClient _vlcHost;
        private bool _useHost;
        //private DispatcherTimer _overlayAutoHideTimer;

        public VideoPlayer()
        {
            InitializeComponent();

            _mediaPlayer = new MediaPlayer(_libVLC);
            // try to start external host helper (if present)
            try
            {
                _vlcHost = new VlcHostClient();
                _useHost = _vlcHost.EnsureStarted();
            }
            catch { _useHost = false; }
            // subscribe to error events to recover from playback errors
            _mediaPlayer.EncounteredError += MediaPlayer_EncounteredError;
            this.DataContextChanged += (sender, e) =>
            {
                if (_viewModel != null)
                {
                    try { _viewModel.SourceUrlChangedEvent -= _viewModel_SourceUrlChangedEvent; } catch { }
                }

                _viewModel = e.NewValue as PlayerViewModel;
                if (_viewModel != null)
                {
                    _viewModel.SourceUrlChangedEvent += _viewModel_SourceUrlChangedEvent;
                }
            };

            VideoView.Loaded += (sender, e) =>
            {
                VideoView.MediaPlayer = _mediaPlayer;
                VideoView.MouseLeftButtonDown += VideoView_MouseLeftButtonDown;
                if (VideoView.MediaPlayer != null)
                    VideoView.MediaPlayer.EnableMouseInput = false;
                VideoView.PreviewMouseLeftButtonDown += VideoView_MouseLeftButtonDown;
                AutoPlay();
            };
            Unloaded += VideoPlayer_Unloaded;
        }


        private void MediaPlayer_EncounteredError(object? sender, EventArgs e)
        {
            // MediaPlayer.EncounteredError can be raised on a native thread; marshal to UI thread
            try
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Diagnostics.Debug.WriteLine("[VideoPlayer] MediaPlayer encountered error — attempting recovery");
                    try { VideoView.MediaPlayer?.Stop(); } catch { }
                    try { VideoView.MediaPlayer?.Media?.Dispose(); } catch { }

                    // dispose existing player and try full LibVLC restart to recover native/network resources
                    try { _mediaPlayer.EncounteredError -= MediaPlayer_EncounteredError; } catch { }
                    try { VideoView.MediaPlayer?.Stop(); } catch { }
                    try { VideoView.MediaPlayer?.Media?.Dispose(); } catch { }
                    try { _mediaPlayer?.Dispose(); } catch { }

                    try { LibVLCManager.Dispose(); } catch { }

                    // recreate LibVLC and MediaPlayer
                    var lib = LibVLCManager.Instance;
                    _mediaPlayer = new MediaPlayer(lib);
                    _mediaPlayer.EncounteredError += MediaPlayer_EncounteredError;

                    // re-assign to the view and restart playback
                    VideoView.MediaPlayer = _mediaPlayer;
                    AutoPlay();
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoPlayer] Error handling encountered error: {ex}");
            }
        }
   

        private void _viewModel_SourceUrlChangedEvent(string videoURL)
        {
            SourceUrl = videoURL;
            try
            {
                VideoView.MediaPlayer?.Stop();
                VideoView.MediaPlayer?.Media?.Dispose();
            }
            catch { }

            AutoPlay();
        }

        private void VideoView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ToggleOverlay();
        }

        private void VideoPlayer_Unloaded(object sender, RoutedEventArgs e)
        {
            // Dispose the VideoView (releases native view references) but keep the shared LibVLC instance
            try { VideoView.Dispose(); } catch { }
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
            if (VideoView?.MediaPlayer == null) return;

            if (!VideoView.MediaPlayer.IsPlaying && !string.IsNullOrWhiteSpace(SourceUrl))
            {
                using (var media = new Media(_libVLC, new Uri(SourceUrl)))
                    VideoView.MediaPlayer.Play(media);
            }
        }
        private void MyUserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ToggleOverlay();
        }
       
        private void ToggleOverlay()
        {
            if (overlayPanel.Visibility == Visibility.Visible)
            {
                if (_viewModel != null)
                    _viewModel.ProgramGuideVisible = false;
                HideOverlay();
            }
            else
            {               
                ShowOverlay();
            }
        }

        private void _overlayAutoHideTimer_Tick(object? sender, EventArgs e)
        {
            HideOverlay();
        }

        public void ShowOverlay()
        {
            //_overlayAutoHideTimer.Start();
            overlayPanel.Visibility = Visibility.Visible;
        }
        public void HideOverlay()
        {
           // _overlayAutoHideTimer.Stop();
            overlayPanel.Visibility = Visibility.Collapsed;
        }
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            try { if(VideoView.MediaPlayer.IsPlaying) VideoView.MediaPlayer?.Stop(); } catch { }
            try { _mediaPlayer?.Dispose(); } catch { }
            try { if (_viewModel != null) _viewModel.SourceUrlChangedEvent -= _viewModel_SourceUrlChangedEvent; } catch { }
            try { VideoView.MediaPlayer?.Dispose(); } catch { }
        }
    }
}
