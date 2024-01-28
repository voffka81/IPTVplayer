using CommunityToolkit.Mvvm.Input;
using System.Reactive.Linq;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player
{

    public class PlayerViewModel : ObservableViewModelBase, IDisposable
    {
        public delegate void SourceUrlChanged(string videoURL);
        public event SourceUrlChanged SourceUrlChangedEvent;

        private M3UInfo _currentProgram;       

        private string _topPaneTitle;
        public string TopPanelTitle
        {
            get => _topPaneTitle;
            set => SetProperty(ref _topPaneTitle, value);
        }

        private int _durationValue;
        public int DurationValue
        {
            get => _durationValue;
            set => SetProperty(ref _durationValue, value);
        }

        private string _programGuide;
        public string ProgramGuideText
        {
            get => _programGuide;
            set => SetProperty(ref _programGuide, value);
        }

        private string _startProgram;
        public string StartProgram
        {
            get => _startProgram;
            set => SetProperty(ref _startProgram, value);
        }

        private string _endProgram;
        public string EndProgram
        {
            get => _endProgram;
            set => SetProperty(ref _endProgram, value);
        }

        private List<M3UInfo> _programs;
        public ICommand BackCommand { get; }
        public ICommand FullscreenCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }

        private ProgramGuide _currentGuide;
        private ProgramInfo _currentProgramInfo;

        private IDisposable _programGuideDisposable;
        private IDisposable _timer;
        private IDisposable _programSubscriber;

        private int _currentProgramIndex = 0;
        public PlayerViewModel(M3UInfo selectedProgram)
        {
            _currentProgram = selectedProgram;

            BackCommand = new RelayCommand(OnButtonBackClick);
            NextCommand = new RelayCommand(NextProgram);
            PreviousCommand = new RelayCommand(PreviousProgram);
            FullscreenCommand = new RelayCommand(TVPlayerViewModel.Instance.FullScreenToggle);

            _programSubscriber = ProgramsData.Instance.AllPrograms.Subscribe(x =>
            {
                _programs = x.Where(p => p.GroupTitle == _currentProgram.GroupTitle).ToList();
                _currentProgramIndex = _programs.Select((program, index) => new { program, index })
                .Where(x => x.program.Name == _currentProgram.Name)
                .Select(x => x.index)
                .FirstOrDefault();
            });
           
            UpdateUI();
        }

        private void UpdateUI()
        {
            TVPlayerViewModel.Instance.TopPanelVisible(false, _currentProgram.Name);
            TopPanelTitle = _currentProgram.Name;
            SourceUrlChangedEvent?.Invoke(_currentProgram.Url);

            _programGuideDisposable = ProgramsData.Instance.ProgramGuideInfo.Subscribe(x =>
            {
                try
                {
                    _currentGuide = x.FirstOrDefault(p => p.Id == _currentProgram.TvgID);
                    UpdateScreenInfo();

                    _timer = Observable.Interval(TimeSpan.FromMinutes(1)).Subscribe(x =>
                    {
                        UpdateScreenInfo();
                    });
                }
                catch { }
            });
        }

        private void PreviousProgram()
        {
            _currentProgramIndex -= 1;
            if (_currentProgramIndex < 0)
                _currentProgramIndex = _programs.Count - 1;
            _currentProgram = _programs[_currentProgramIndex];
            _programGuideDisposable?.Dispose();
            UpdateUI();
        }
        private void NextProgram()
        {
            _currentProgramIndex += 1;
            if (_currentProgramIndex > _programs.Count - 1)
                _currentProgramIndex = 0;
            _currentProgram = _programs[_currentProgramIndex];
            _programGuideDisposable?.Dispose();
            UpdateUI();
        }
        private void UpdateScreenInfo()
        {
            _currentProgramInfo = _currentGuide.Programs.FirstOrDefault(d => d.StartTime <= DateTime.Now && d.StopTime >= DateTime.Now);
            if (_currentProgramInfo.Title != ProgramGuideText)
            {
                ProgramGuideText = _currentProgramInfo.Title;
                StartProgram = _currentProgramInfo.StartTime.ToShortTimeString();
                EndProgram = _currentProgramInfo.StopTime.ToShortTimeString();
            }
            var programMinutes = (_currentProgramInfo.StopTime - _currentProgramInfo.StartTime).TotalMinutes;
            DurationValue = (int)((DateTime.Now - _currentProgramInfo.StartTime).TotalMinutes / programMinutes * 100);
        }
        private void OnButtonBackClick()
        {
            var groupInfo = new GroupInfo() { Name = _currentProgram.GroupTitle, Count = 0 };

            var programListViewModel = new ProgramsListViewModel(groupInfo);
            var conrtrol = new ProgramsList();
            TVPlayerViewModel.Instance.SetPageContext(conrtrol, programListViewModel);
        }
        public void Dispose()
        {
            _programSubscriber.Dispose();
            _programGuideDisposable.Dispose();
            _timer.Dispose();
        }
    }
}
