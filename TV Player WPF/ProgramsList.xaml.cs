using System.Windows.Controls;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for ProgramsGroupGrid.xaml
    /// </summary>
    public partial class ProgramsList : UserControl
    {
        public ProgramsList()
        {
            InitializeComponent();
        }
        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ProgramsListViewModel viewModel)
            {
                viewModel.ItemSelectedCommand.Execute(null);
            }
        }
    }
}
