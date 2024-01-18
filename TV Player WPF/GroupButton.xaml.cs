using System.Windows.Controls;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for GroupButton.xaml
    /// </summary>
    public partial class GroupButton : UserControl
    {
        public GroupButton(string groupName,int programsFound)
        {
            InitializeComponent();
            this.groupName.Text = groupName;
            this.programsFound.Text = programsFound.ToString();
        }
    }
}
