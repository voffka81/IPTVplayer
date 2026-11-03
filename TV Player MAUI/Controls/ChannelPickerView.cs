using TV_Player;

namespace TV_Player.Maui.Controls;

/// <summary>Renders and navigates the Android channel picker without owning playback.</summary>
public sealed class ChannelPickerView : ContentView
{
    private readonly ScrollView _scroll;
    private readonly HorizontalStackLayout _items = new() { Spacing = 8 };
    private readonly List<(M3UInfo Channel, Border Tile, Button Button)> _channelPickerTiles = new();
    private IReadOnlyList<M3UInfo> _channels = Array.Empty<M3UInfo>();
    private M3UInfo? _selectedChannel;
    private int _focusedIndex = -1;
    private int _focusGeneration;

    public event Action<M3UInfo>? ChannelSelected;

    public ChannelPickerView()
    {
        _scroll = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _items };
        Content = _scroll;
    }

    public void SetChannels(IReadOnlyList<M3UInfo> channels)
    {
        CancelPendingFocus();
        _focusedIndex = -1;
        _channels = channels;
        _channelPickerTiles.Clear();
        _items.Children.Clear();
        foreach (var channel in channels)
            _items.Children.Add(CreateChannelPickerTile(channel));
        UpdateChannelPickerSelection();
    }

    public void SelectChannel(M3UInfo channel)
    {
        _selectedChannel = channel;
        _focusedIndex = ChannelSelection.FindIndex(_channels, channel);
        UpdateChannelPickerSelection();
    }

    private View CreateChannelPickerTile(M3UInfo channel)
    {
        var logoContainer = new Grid { HeightRequest = 44, WidthRequest = 76 };

        if (Uri.TryCreate(channel.Logo, UriKind.Absolute, out var logoUri) &&
            (logoUri.Scheme == Uri.UriSchemeHttp || logoUri.Scheme == Uri.UriSchemeHttps))
        {
            logoContainer.Children.Add(new Image
            {
                Source = new UriImageSource
                {
                    Uri = logoUri,
                    CachingEnabled = true,
                    CacheValidity = TimeSpan.FromDays(7)
                },
                Aspect = Aspect.AspectFit,
                InputTransparent = true
            });
        }

        var nameLabel = new Label
        {
            Text = channel.Name,
            FontSize = 12,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        var content = new VerticalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true,
            Children = { logoContainer, nameLabel }
        };
        var tile = new Border
        {
            WidthRequest = 112,
            HeightRequest = 88,
            Padding = new Thickness(8, 5),
            BackgroundColor = Color.FromArgb("#162130"),
            Stroke = Color.FromArgb("#304054"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = new Grid { Children = { content } }
        };
        var button = new Button
        {
            BackgroundColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            BorderWidth = 0,
            CornerRadius = 10,
            Padding = 0,
            Text = string.Empty
        };
        button.Clicked += (_, _) =>
        {
            ChannelSelected?.Invoke(channel);
        };
        _channelPickerTiles.Add((channel, tile, button));
        UpdateChannelPickerSelection();
        button.Focused += (_, _) =>
        {
            _focusedIndex = _channelPickerTiles.FindIndex(item => item.Button == button);
            tile.Stroke = Color.FromArgb("#7BD0FF");
            tile.StrokeThickness = 2;
        };
        button.Unfocused += (_, _) => UpdateChannelPickerSelection();
        ((Grid)tile.Content).Children.Add(button);
        return tile;
    }

    public async Task ScrollSelectedChannelIntoViewAsync()
    {
        for (var attempt = 0; attempt < 8 && _scroll.ContentSize.Width <= _scroll.Width; attempt++)
            await Task.Delay(50);
        var selectedIndex = _selectedChannel == null ? -1 : ChannelSelection.FindIndex(_channels, _selectedChannel);
        var selectedTile = selectedIndex >= 0 && selectedIndex < _channelPickerTiles.Count
            ? _channelPickerTiles[selectedIndex].Tile
            : null;
        if (selectedTile == null || _scroll.Width <= 0) return;

        var maxOffset = Math.Max(0, _scroll.ContentSize.Width - _scroll.Width);
        var offset = Math.Clamp(selectedTile.X + selectedTile.Width / 2 - _scroll.Width / 2, 0, maxOffset);
        await _scroll.ScrollToAsync(offset, 0, animated: false);
    }

    public void CancelPendingFocus() => _focusGeneration++;

    public async Task FocusSelectedChannelAsync()
    {
        var generation = ++_focusGeneration;
        await ScrollSelectedChannelIntoViewAsync();
        if (generation != _focusGeneration || !IsVisible || _channelPickerTiles.Count == 0) return;
        var index = _selectedChannel == null ? -1 : ChannelSelection.FindIndex(_channels, _selectedChannel);
        _focusedIndex = Math.Max(0, index);
        _channelPickerTiles[_focusedIndex].Button.Focus();
    }

    public async Task MoveFocusAsync(int direction)
    {
        if (!IsVisible || _channelPickerTiles.Count == 0) return;
        var generation = ++_focusGeneration;
        _focusedIndex = ChannelSelection.NextIndex(_focusedIndex, _channelPickerTiles.Count, direction);
        var item = _channelPickerTiles[_focusedIndex];
        item.Button.Focus();
        await _scroll.ScrollToAsync(item.Tile, ScrollToPosition.Center, animated: false);
        if (generation == _focusGeneration && IsVisible) item.Button.Focus();
    }

    private void UpdateChannelPickerSelection()
    {
        foreach (var (channel, tile, button) in _channelPickerTiles)
        {
            var isSelected = _selectedChannel != null && ChannelSelection.IsSameChannel(channel, _selectedChannel);
            tile.BackgroundColor = Color.FromArgb(isSelected ? "#1A4263" : "#162130");
            tile.Stroke = Color.FromArgb(button.IsFocused ? "#7BD0FF" : isSelected ? "#55B9FF" : "#304054");
            tile.StrokeThickness = isSelected || button.IsFocused ? 2 : 1;
        }
    }

}
