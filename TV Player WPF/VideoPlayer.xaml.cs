using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for ProgramsGroupGrid.xaml
    /// </summary>
    public partial class VideoPlayer : UserControl
    {
        private readonly DirectoryInfo vlcLibDirectory;
        
        public static readonly DependencyProperty SourceUrlProperty =
            DependencyProperty.Register(
                "SourceUrl", // Name of the property
                typeof(string), // Type of the property
                typeof(VideoPlayer), // Type of the owner class
                new PropertyMetadata(string.Empty) // Default value
            );

        LibVLC _libVLC;
        MediaPlayer _mediaPlayer;

        public string SourceUrl
        {
            get { return (string)GetValue(SourceUrlProperty); }
            set { SetValue(SourceUrlProperty, value);}
        }
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
            VideoView.MediaPlayer.Dispose();
        }
    }
}
