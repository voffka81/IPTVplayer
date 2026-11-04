using System.Globalization;
using System.Text.Json;

namespace TV_Player.Localization;

public static class I18n
{
    private static readonly Dictionary<string, Dictionary<string, string>> Resources = Load();
    public static string Language { get; private set; } = Normalize(CultureInfo.CurrentUICulture.Name);
    public static IReadOnlyList<string> LanguageNames { get; } = new[] { "Русский", "English" };
    public static string Normalize(string? language) =>
        (language ?? CultureInfo.CurrentUICulture.Name).StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
    public static void SetLanguage(string? language) => Language = Normalize(language);
    public static string Get(string key) => Resources.TryGetValue(key, out var entry)
        ? entry[Language] : key;
    public static string TranslateKnown(string text) =>
        Resources.Values.FirstOrDefault(entry => entry["en"] == text || entry["ru"] == text)?[Language] ?? text;
    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.GetCultureInfo(Language), Get(key), args);
    private static Dictionary<string, Dictionary<string, string>> Load()
    {
        using var stream = typeof(I18n).Assembly.GetManifestResourceStream("TV_Player.Localization.translations.json")
            ?? throw new InvalidOperationException("Missing localization resources.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateObject().ToDictionary(
            resource => resource.Name,
            resource => resource.Value.EnumerateObject().ToDictionary(
                translation => translation.Name, translation => translation.Value.GetString()!));
    }
}
