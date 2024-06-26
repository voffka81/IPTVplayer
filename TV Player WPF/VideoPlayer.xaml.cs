﻿using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for ProgramsGroupGrid.xaml
    /// </summary>
    public partial class VideoPlayer : UserControl
    {
        public string SourceUrl { get; set; }

        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private PlayerViewModel _viewModel;
        //private DispatcherTimer _overlayAutoHideTimer;
        
        public VideoPlayer()
        {
            InitializeComponent();

            //_overlayAutoHideTimer = new DispatcherTimer();
            //_overlayAutoHideTimer.Interval = TimeSpan.FromSeconds(3);
            //_overlayAutoHideTimer.Tick += _overlayAutoHideTimer_Tick;
            //_overlayAutoHideTimer.Start();

            _libVLC = new LibVLC(enableDebugLogs: true);
            _mediaPlayer = new MediaPlayer(_libVLC);
            this.DataContextChanged += (sender, e) =>
            {
                _viewModel = (PlayerViewModel)e.NewValue;
                _viewModel.SourceUrlChangedEvent += _viewModel_SourceUrlChangedEvent;
            };

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

        private void _viewModel_SourceUrlChangedEvent(string videoURL)
        {
            SourceUrl = videoURL;
            VideoView.MediaPlayer.Stop();
            VideoView.MediaPlayer.Media.Dispose();

            AutoPlay();
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
       
        private void ToggleOverlay()
        {
            if (overlayPanel.Visibility == Visibility.Visible)
            {
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
            _viewModel.SourceUrlChangedEvent -= _viewModel_SourceUrlChangedEvent;
            VideoView.MediaPlayer?.Dispose();
        }
    }
}
