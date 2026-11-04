using System.Globalization;

namespace WavyFi.Localization;

/// <summary>Shared presentation language for the GUI, CLI and advice engine.
/// Protocol identifiers and scan data must never be translated.</summary>
public static class Locale
{
    private static CultureInfo _culture = CultureInfo.GetCultureInfo("en");

    public static CultureInfo Culture
    {
        get => _culture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _culture = value.TwoLetterISOLanguageName == "zh"
                ? CultureInfo.GetCultureInfo("zh-CN")
                : CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentUICulture = _culture;
            CultureInfo.DefaultThreadCurrentUICulture = _culture;
        }
    }

    public static bool IsChinese => Culture.TwoLetterISOLanguageName == "zh";

    public static string T(string english, string chinese) => IsChinese ? chinese : english;

    public static string F(string english, string chinese, params object?[] args) =>
        string.Format(Culture, T(english, chinese), args);
}
