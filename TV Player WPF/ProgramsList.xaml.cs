using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for ProgramsGroupGrid.xaml
    /// </summary>
    public partial class ProgramsList : UserControl
    {
        private Point _mousePosition;
        private bool _isDragging;
        private DispatcherTimer clickTimer;

        public ProgramsList()
        {
            InitializeComponent();
            clickTimer = new DispatcherTimer();
            clickTimer.Interval = TimeSpan.FromMilliseconds(200); // Adjust the interval as needed
            clickTimer.Tick += ClickTimer_Tick;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isDragging)
            {
                e.Handled = true; // Prevent selection change during drag-and-drop
            }
            else if (DataContext is ProgramsListViewModel viewModel)
            {
                viewModel.ItemSelectedCommand.Execute(null);
            }
        }

        private void ListView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _mousePosition = e.GetPosition(null);
        }

        private void ListView_MouseMove(object sender, MouseEventArgs e)
        {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Point currentPosition = e.GetPosition(null);

                    if (!_isDragging && (Math.Abs(currentPosition.X - _mousePosition.X) > SystemParameters.MinimumHorizontalDragDistance ||
                                        Math.Abs(currentPosition.Y - _mousePosition.Y) > SystemParameters.MinimumVerticalDragDistance))
                    {
                    _isDragging = true;
                        e.Handled = true;
                    }

                    if (_isDragging)
                    {
                        // Get the ListView scroll viewer
                        ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(programsList);

                        if (scrollViewer != null)
                        {
                            double verticalScrollPosition = currentPosition.Y - _mousePosition.Y;
                            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - verticalScrollPosition);
                        }
                    }
                }
        }
        
        private void ListView_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDragging = false;
            clickTimer.Stop();

            if (IsClick())
            {
                // Perform selection logic here
                ListViewItem listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
                if (listViewItem != null)
                {
                    listViewItem.IsSelected = true;
                    if (DataContext is ProgramsListViewModel viewModel)
                    {
                        viewModel.ItemSelectedCommand.Execute(null);
                    }
                }
            }
        }

        private void ClickTimer_Tick(object sender, EventArgs e)
        {
            clickTimer.Stop();
        }

        private bool IsClick()
        {
            Point currentPosition = Mouse.GetPosition(null);
            return Math.Abs(currentPosition.X - _mousePosition.X) < SystemParameters.MinimumHorizontalDragDistance &&
                   Math.Abs(currentPosition.Y - _mousePosition.Y) < SystemParameters.MinimumVerticalDragDistance;
        }

        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                {
                    return (T)child;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T ancestor)
                {
                    return ancestor;
                }
                current = VisualTreeHelper.GetParent(current);
            } while (current != null);

            return null;
        }
    }
}
