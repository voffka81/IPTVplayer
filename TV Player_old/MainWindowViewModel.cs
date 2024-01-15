using System.Windows.Controls;

namespace TV_Player
{
    public class MainWindowViewModel
    {
        public MainWindowViewModel()
        {
        }

        private async Task Initialize()
        {
            string m3uLink = "http://pl.da-tv.vip/a71e77fa/835b3216/tv.m3u";
            var programs = await M3UParser.DownloadM3UFromWebAsync(m3uLink);
            var groupedPrograms = programs.GroupBy(item => item.GroupTitle)
                               .ToDictionary(
                                   group => group.Key,
                                   group => group.Select(item => item).ToList()
                               );

            var row = 0;
            var column = 0;

            foreach (var group in groupedPrograms)
            {
                var button = new GroupButton(group.Key, group.Value.Count());
                button.TouchDown += (s, e) =>
                {

                };
                Grid.SetRow(button, row);
                Grid.SetColumn(button, column);
                groupsGrid.Children.Add(button);
                column++;
                if (column > 4)
                {
                    row++;
                    column = 0;
                }
            }
        }
    }
}
