using System.Windows.Controls;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for ProgramsGroupGrid.xaml
    /// </summary>
    public partial class PlaylistsGroup : UserControl
    {
        public PlaylistsGroup()
        {
            InitializeComponent();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is PlaylistsGroupViewModel viewModel)
            {
                viewModel.ItemSelectedCommand.Execute(null);
            }
        }
    }
}
