using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TV_Player.Localization;
using Xunit;

namespace TV_Player.Tests;

[CollectionDefinition("Localization", DisableParallelization = true)]
public sealed class LocalizationCollection;

[Collection("Localization")]
public sealed class LocalizationTests
{
    [Theory]
    [InlineData("ru-RU", "ru")]
    [InlineData("ru", "ru")]
    [InlineData("en-GB", "en")]
    [InlineData("he-IL", "en")]
    public void LanguagesNormalizeToSupportedChoices(string culture, string expected)
        => Assert.Equal(expected, I18n.Normalize(culture));

    [Fact]
    public void BothLanguagesCoverEveryResourceAndPreserveFormatArguments()
    {
        using var stream = typeof(I18n).Assembly.GetManifestResourceStream("TV_Player.Localization.translations.json")!;
        var resources = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
        Assert.True(resources.Count > 100);
        foreach (var (key, translations) in resources)
        {
            Assert.False(string.IsNullOrWhiteSpace(translations["ru"]), key);
            Assert.False(string.IsNullOrWhiteSpace(translations["en"]), key);
            string[] Arguments(string text) => Regex.Matches(text, @"\{(\d+)(?:[^}]*)\}")
                .Select(match => match.Groups[1].Value).Order().ToArray();
            Assert.Equal(Arguments(translations["en"]), Arguments(translations["ru"]));
            foreach (var language in new[] { "ru", "en" })
                _ = string.Format(CultureInfo.GetCultureInfo(language), translations[language], 1.5, 2.5, 3.5);
        }
    }

    [Fact]
    public void SwitchingLanguageChangesLabelsAndCanBeRestored()
    {
        var original = I18n.Language;
        try
        {
            I18n.SetLanguage("ru");
            Assert.Equal("Язык", I18n.Get("Ta4fe65264ef7"));
            I18n.SetLanguage("en");
            Assert.Equal("Language", I18n.Get("Ta4fe65264ef7"));
            Assert.Equal("external provider text", I18n.Get("external provider text"));
        }
        finally { I18n.SetLanguage(original); }
    }
}
