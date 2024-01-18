using System.Windows.Input;

namespace TV_Player
{
    
    public class PlayerViewModel: ObservableViewModelBase
    {

        private readonly M3UInfo _currentProgram;

        private string _urlSource;
        public string URLSource
        {
            get => _urlSource;
            set => SetProperty(ref _urlSource, value);
        }

        public GroupInfo SelectedItem { get; set; }
        public ICommand PlayCommand { get; }

        public PlayerViewModel(M3UInfo selectedProgram)
        {
            _currentProgram = selectedProgram;
            //PlayM3U8(_currentProgram.Url);
            //_libVLC = new LibVLC();
            //_mediaPlayer = new MediaPlayer(new Media(_libVLC, new Uri(_currentProgram.Url)));
            //_mediaPlayer.Play();
            //PlayCommand = new Command(OnPlayButtonClicked);
        }

        private void OnPlayButtonClicked()
        {
            PlayM3U8(_currentProgram.Url);
        }

        private void PlayM3U8(string url)
        {
            try
            {
                URLSource = _currentProgram.Url;
            }
            catch (Exception ex)
            {
                // Handle exceptions
            }
        }
    }
}
