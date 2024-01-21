using CommunityToolkit.Mvvm.Input;
using System.Reactive.Linq;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player
{

    public class PlayerViewModel : ObservableViewModelBase, IDisposable
    {
        private M3UInfo _currentProgram;
        public M3UInfo SelectedProgram
        {
            get => _currentProgram;
            set => SetProperty(ref _currentProgram, value);
        }

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


        public M3UInfo SelectedItem { get; set; }
        public ICommand BackCommand { get; }

        private ProgramGuide _currentGuide;
        private ProgramInfo _currentProgramInfo;

        private IDisposable _programGuideDisposable;
        private IDisposable _timer;
        public PlayerViewModel(M3UInfo selectedProgram)
        {
            _currentProgram = selectedProgram;
            BackCommand = new RelayCommand(OnButtonBackClick);
            TVPlayerViewModel.Instance.TopPanelVisible(false, selectedProgram.Name);

            TopPanelTitle = selectedProgram.Name;

            _programGuideDisposable = ProgramsData.Instance.ProgramGuideInfo.Subscribe(x =>
                {
                    try
                    {
                        _currentGuide = x.FirstOrDefault(p => p.Id == selectedProgram.TvgID);
                        UpdateScreenInfo();

                        _timer = Observable.Interval(TimeSpan.FromMinutes(1)).Subscribe(x =>
                        {
                            UpdateScreenInfo();
                        });
                    }
                    catch { }
                });

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
            _programGuideDisposable.Dispose();
            _timer.Dispose();
        }
    }
}
