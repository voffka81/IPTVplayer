using System.Windows.Controls;
using System.Windows;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for ProgramsGroupGrid.xaml
    /// </summary>
    public partial class Settings : UserControl
    {
        public Settings()
        {
            InitializeComponent();
        }

        private void OpenAddPlayList_Click(object sender, System.Windows.RoutedEventArgs e) => AddPlayList.Visibility = Visibility.Visible;

        private void AddPlayList_Click(object sender, RoutedEventArgs e) => AddPlayList.Visibility = Visibility.Collapsed;
    }
}
