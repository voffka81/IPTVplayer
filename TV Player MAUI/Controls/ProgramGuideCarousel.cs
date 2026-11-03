using TV_Player.Presentation;

namespace TV_Player.Maui.Controls;

/// <summary>Owns the compact, horizontally scrolling Android program guide cards.</summary>
public sealed class ProgramGuideCarousel : ContentView
{
    private readonly HorizontalStackLayout _items = new() { Spacing = 8 };
    private readonly List<(EpgProgramItem Program, Border Card, ProgressBar Progress, Button Button)> _cards = new();
    private readonly ScrollView _scroll;
    private int _focusedIndex;
    private int _focusGeneration;

    public ProgramGuideCarousel()
    {
        _scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = _items
        };
        Content = _scroll;
    }

    public void Clear()
    {
        CancelPendingFocus();
        _focusedIndex = 0;
        _cards.Clear();
        _items.Children.Clear();
    }

    public void SetPrograms(IReadOnlyList<EpgProgramItem> programs)
    {
        var sameWindow = programs.Count == _cards.Count && programs.Select((item, index) =>
            SameProgram(item, _cards[index].Program)).All(same => same);
        if (sameWindow)
        {
            for (var i = 0; i < programs.Count; i++)
            {
                var card = _cards[i];
                _cards[i] = (programs[i], card.Card, card.Progress, card.Button);
            }
            RenderStyles();
            return;
        }
        var focused = _cards.FirstOrDefault(card => card.Button.IsFocused).Program;
        Clear();
        foreach (var program in programs) AddCard(program);
        RenderStyles();
        if (focused != null && IsVisible && _cards.Count > 0)
        {
            _focusedIndex = Math.Max(0, _cards.FindIndex(card => SameProgram(card.Program, focused)));
            _ = MoveFocusAsync(0);
        }
    }

    private static bool SameProgram(EpgProgramItem left, EpgProgramItem right) =>
        left.StartTime == right.StartTime && left.EndTime == right.EndTime && left.Title == right.Title;

    private void RenderStyles()
    {
        foreach (var (program, card, progress, button) in _cards)
        {
            card.BackgroundColor = Color.FromArgb(program.IsCurrent ? "#1A4263" : "#162130");
            card.Stroke = Color.FromArgb(button.IsFocused ? "#7BD0FF" : program.IsCurrent ? "#55B9FF" : "#304054");
            card.StrokeThickness = program.IsCurrent || button.IsFocused ? 2 : 1;
            progress.IsVisible = program.IsCurrent;
            progress.Progress = program.Progress;
        }
    }

    public void CancelPendingFocus() => _focusGeneration++;

    public void FocusFirstProgram()
    {
        if (IsVisible && _cards.Count > 0)
        {
            _focusedIndex = 0;
            _cards[0].Button.Focus();
            _ = _scroll.ScrollToAsync(0, 0, animated: false);
        }
    }

    public async Task MoveFocusAsync(int direction)
    {
        if (!IsVisible || _cards.Count == 0) return;
        var generation = ++_focusGeneration;
        _focusedIndex = Math.Clamp(_focusedIndex + Math.Sign(direction), 0, _cards.Count - 1);
        var item = _cards[_focusedIndex];
        item.Button.Focus();
        await _scroll.ScrollToAsync(item.Card, ScrollToPosition.Center, animated: false);
        if (generation == _focusGeneration && IsVisible) item.Button.Focus();
    }

    private void AddCard(EpgProgramItem program)
    {
        var timeLabel = new Label
        {
            Text = $"{program.TimeLabels.Start} – {program.TimeLabels.End}",
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
        var button = new Button
        {
            BackgroundColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            BorderWidth = 0,
            CornerRadius = 10,
            Text = string.Empty,
            Padding = 0
        };
        button.Focused += (_, _) =>
        {
            _focusedIndex = _cards.FindIndex(item => item.Button == button);
            RenderStyles();
        };
        button.Unfocused += (_, _) => RenderStyles();
        var content = (View)card.Content;
        content.InputTransparent = true;
        card.Content = new Grid { Children = { content, button } };
        _cards.Add((program, card, progress, button));
        _items.Children.Add(card);
    }
}
