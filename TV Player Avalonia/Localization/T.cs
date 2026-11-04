using Avalonia.Markup.Xaml;

namespace TV_Player.AvaloniaApp.Localization;

public sealed class T : MarkupExtension
{
    public string Key { get; set; } = string.Empty;
    public override object ProvideValue(IServiceProvider serviceProvider) => TV_Player.Localization.I18n.Get(Key);
}
