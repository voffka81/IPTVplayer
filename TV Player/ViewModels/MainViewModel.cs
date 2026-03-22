using System.Windows.Input;
using System.Reactive.Disposables;

namespace TV_Player.MAUI
{
    public class MainViewModel : ObservableViewModelBase, IDisposable
    {
        private CompositeDisposable _disposables = new CompositeDisposable();
        private List<GroupInfo> _programs;
        public List<GroupInfo> Programs
        {
            get => _programs;
            set => SetProperty(ref _programs, value);
        }

        public GroupInfo SelectedItem { get; set; }
        public ICommand ItemSelectedCommand { get; }

        public MainViewModel()
        {
            ItemSelectedCommand = new Command(OnItemSelected);
            _disposables.Add(
                TVPlayerViewModel.Instance.PlaylistData.GroupsInformation.Subscribe(x => Programs = x)
            );
        }

        private void OnItemSelected()
        {
            try
            {
                if (Application.Current?.MainPage?.Navigation == null)
                {
                    System.Diagnostics.Debug.WriteLine("Navigation context is not available");
                    return;
                }

                var programPageViewModel = new ProgramViewModel(SelectedItem);

                var programPage = new ProgramPage
                {
                    BindingContext = programPageViewModel
                };
                
                Application.Current.MainPage.Navigation.PushAsync(programPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}
