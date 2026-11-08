namespace LiteSS;

internal enum OcrProviderKind
{
    Windows,
    Tesseract,
    Python,
}

internal enum TranslationProviderKind
{
    None,
    Google,
    LibreTranslate,
    DeepL,
    PythonArgos,
    PythonLibreTranslate,
}

/// <summary>A language tag and the label shown in the settings window.</summary>
internal sealed record LanguageOption(string Code, string Name)
{
    public override string ToString() => Name;
}

internal static class Languages
{
    private static readonly LanguageOption[] Common =
    {
        new("en", "English"),
        new("de", "German"),
        new("fr", "French"),
        new("es", "Spanish"),
        new("it", "Italian"),
        new("pt", "Portuguese"),
        new("nl", "Dutch"),
        new("pl", "Polish"),
        new("ru", "Russian"),
        new("uk", "Ukrainian"),
        new("tr", "Turkish"),
        new("ja", "Japanese"),
        new("zh", "Chinese (Simplified)"),
        new("ko", "Korean"),
        new("ar", "Arabic"),
        new("hi", "Hindi"),
    };

    public static IReadOnlyList<LanguageOption> Ocr { get; } =
        new[] { new LanguageOption("auto", "Auto (system language)") }
            .Concat(Common)
            .ToArray();

    public static IReadOnlyList<LanguageOption> TranslationSource { get; } =
        new[] { new LanguageOption("auto", "Auto-detect") }
            .Concat(Common)
            .ToArray();

    public static IReadOnlyList<LanguageOption> TranslationTarget { get; } = Common;

    public static LanguageOption Find(IReadOnlyList<LanguageOption> options, string? code)
    {
        var match = options.FirstOrDefault(option =>
            string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase));
        return match ?? options[0];
    }

    public static string NameOf(string? code)
    {
        foreach (var option in TranslationSource)
        {
            if (string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return option.Name;
            }
        }

        return string.IsNullOrWhiteSpace(code) ? "Unknown" : code;
    }
}
