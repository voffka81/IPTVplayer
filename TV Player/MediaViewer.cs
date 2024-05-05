
namespace TV_Player.MAUI
{
    public class MediaViewer : ContentView
    {
        //private MediaPlayer _mediaPlayer;

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

        public MediaViewer()
        {
            InitializeMediaPlayer();
        }

        private void InitializeMediaPlayer()
        {
           // var media = new Media(_libVLC, new Uri("http://ost.da-tv.vip/uPVtzdGJfdG9rZW5dIiwibCI6ImE3MWU3N2ZhIiwicCI6ImE3MWU3N2ZhODM1YjMyMTYiLCJjIjoiNDk3IiwidCI6ImUzNjAwZTEwZmFmMGVhYjhhYWY1YTU2YzRkN2VjZTE5IiwiZCI6IjIzMTQ2IiwiciI6IjIzMDM4IiwibSI6InR2IiwiZHQiOiIwIn0eyJ1IjoiaHR0cDovLzQ1LjkzLjQ2LjI3Ojg4ODcvODM2MS92aWRlby5tM3U4P3Rva2V/video.m3u8"));
            //_mediaPlayer.Play();
        }

        public void Play()
        {
           
        }
    }
}
