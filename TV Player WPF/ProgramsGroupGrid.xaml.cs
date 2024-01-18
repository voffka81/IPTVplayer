using System.Windows.Controls;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for ProgramsGroupGrid.xaml
    /// </summary>
    public partial class ProgramsGroupGrid : UserControl
    {
        public ProgramsGroupGrid()
        {
            InitializeComponent();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ProgramsGroupViewModel viewModel)
            {
                viewModel.ItemSelectedCommand.Execute(null);
            }
        }
    }
}
