using TV_Player;

namespace TV_Player.Maui.Controls;

/// <summary>Owns the compact, horizontally scrolling Android program guide cards.</summary>
public sealed class ProgramGuideCarousel : ContentView
{
    private readonly HorizontalStackLayout _items = new() { Spacing = 8 };
    private readonly List<(ProgramInfo Program, Border Card, ProgressBar Progress)> _cards = new();

    public ProgramGuideCarousel()
    {
        Content = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = _items
        };
    }

    public void Clear()
    {
        _cards.Clear();
        _items.Children.Clear();
    }

    public void SetPrograms(IEnumerable<ProgramInfo> programs)
    {
        Clear();
        foreach (var program in programs)
            AddCard(program);
        Refresh();
    }

    public void Refresh()
    {
        var now = DateTime.Now;
        foreach (var (program, card, progress) in _cards)
        {
            var isCurrent = program.StartTime <= now && now < program.EndTime;
            card.BackgroundColor = Color.FromArgb(isCurrent ? "#1A4263" : "#162130");
            card.Stroke = Color.FromArgb(isCurrent ? "#55B9FF" : "#304054");
            card.StrokeThickness = isCurrent ? 2 : 1;
            progress.IsVisible = isCurrent;
            var duration = (program.EndTime - program.StartTime).TotalSeconds;
            progress.Progress = isCurrent && duration > 0
                ? Math.Clamp((now - program.StartTime).TotalSeconds / duration, 0, 1)
                : 0;
        }
    }

    private void AddCard(ProgramInfo program)
    {
        var timeLabel = new Label
        {
            Text = $"{program.StartTime:HH:mm} – {program.EndTime:HH:mm}",
            FontSize = 12,
            TextColor = Color.FromArgb("#8DD2FF")
        };
        var titleLabel = new Label
        {
            Text = program.Title,
            FontSize = 13,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2,
            WidthRequest = 160
        };
        var progress = new ProgressBar
        {
            Progress = 0,
            ProgressColor = Color.FromArgb("#55B9FF"),
            BackgroundColor = Color.FromArgb("#304054"),
            HeightRequest = 3,
            IsVisible = false
        };
        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#162130"),
            Stroke = Color.FromArgb("#304054"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(10, 6),
            Content = new VerticalStackLayout { Spacing = 2, Children = { timeLabel, titleLabel, progress } }
        };
        _cards.Add((program, card, progress));
        _items.Children.Add(card);
    }
}
