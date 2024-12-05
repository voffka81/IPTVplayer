using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive;
using System.Windows.Input;
using TV_Player.ViewModels;
using System.Windows;
using System.Reactive.Concurrency;

namespace TV_Player
{
    public class ProgramsListViewModel : ObservableViewModelBase, IDisposable
    {
        private ObservableCollection<M3UInfo> _programs = new ObservableCollection<M3UInfo>();
        public ObservableCollection<M3UInfo> Programs
        {
            get => _programs;
            set => SetProperty(ref _programs, value);
        }

        public M3UInfo SelectedItem { get; set; }
        public ICommand ItemSelectedCommand { get; }
        private CancellationTokenSource cts = new CancellationTokenSource();


        public ProgramsListViewModel(string playlistName, GroupInfo groupInfo)
        {
            TVPlayerViewModel.Instance.TopPanelVisible(true, groupInfo.Name);
            ItemSelectedCommand = new RelayCommand(OnItemSelected);
            //_programSubscriber = TVPlayerViewModel.Instance.CurrentProgrmsData.AllPrograms.Subscribe(x => Programs = x.Where(p => p.GroupTitle == groupInfo.Name));

            SubscribeToProgramsData(groupInfo);


            TVPlayerViewModel.Instance.SetBackButtonAction(new Action(() =>
            {
                TVPlayerViewModel.Instance.ShowProgramsGroupScreen(playlistName);
            }));
        }

        private void SubscribeToProgramsData(GroupInfo groupInfo)
        {
             TVPlayerViewModel.Instance.CurrentProgrmsData.AllPrograms.ObserveOn(Scheduler.Default)
                   .Subscribe(newPrograms =>
                   {
                       var filteredPrograms = newPrograms.Where(p => p.GroupTitle == groupInfo.Name).ToList();

                       Programs.Clear();
                       const int batchSize = 100; // Define the batch size
                       int totalItems = filteredPrograms.Count;

                       for (int i = 0; i < totalItems; i += batchSize)
                       {
                           // Take the next batch of 100 items
                           var batch = filteredPrograms.Skip(i).Take(batchSize).ToList();
                           Application.Current.Dispatcher.Invoke(() =>
                           {
                               // Add the batch to the collection
                               foreach (var program in batch)
                               {

                                   Programs.Add(program);

                               }
                           });
                           Task.Delay(500, cts.Token).Wait();
                       }
                   }, cts.Token);
        }

        private void OnItemSelected()
        {
            TVPlayerViewModel.Instance.ShowPlayerScreen(SelectedItem);
        }

        public void Dispose()
        {
            cts.Cancel();
        }
    }
}
