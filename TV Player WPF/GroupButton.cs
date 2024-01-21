using System.Windows;
using System.Windows.Controls;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for GroupButton.xaml
    /// </summary>
    public partial class GroupButton : UserControl
    {
        public static readonly DependencyProperty GroupNameProperty =
            DependencyProperty.Register(
                "GroupName", // Name of the property
                typeof(string), // Type of the property
                typeof(GroupButton), // Type of the owner class
                new PropertyMetadata(string.Empty) // Default value
            );
        public string GroupName
        {
            get { return (string)GetValue(GroupNameProperty); }
            set { SetValue(GroupNameProperty, value); }
        }

        public static readonly DependencyProperty ProgramsCountProperty =
            DependencyProperty.Register(
                "ProgramsCount", // Name of the property
                typeof(string), // Type of the property
                typeof(GroupButton), // Type of the owner class
                new PropertyMetadata(string.Empty) // Default value
            );
        public string ProgramsCount
        {
            get { return (string)GetValue(ProgramsCountProperty); }
            set { SetValue(ProgramsCountProperty, value); }
        }
    }
}
