using Microsoft.Maui.Controls.Xaml;

namespace TV_Player.Maui.Localization;

[ContentProperty(nameof(Key))]
public sealed class T : IMarkupExtension<string>
{
    public string Key { get; set; } = string.Empty;
    public string ProvideValue(IServiceProvider serviceProvider) => TV_Player.Localization.I18n.Get(Key);
    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
