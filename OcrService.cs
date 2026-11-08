using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace LiteSS;

internal static class OcrService
{
    public static Task<string> RecognizeAsync(
        Bitmap image,
        AppSettings settings,
        CancellationToken cancellationToken) =>
        settings.OcrProvider switch
        {
            OcrProviderKind.Python => PythonBridge.RecognizeAsync(image, settings, cancellationToken),
            OcrProviderKind.Tesseract => TesseractOcrProvider.RecognizeAsync(image, settings, cancellationToken),
            _ => WindowsOcrProvider.RecognizeAsync(image, settings.OcrLanguage, cancellationToken),
        };

    public static string DisplayName(OcrProviderKind provider) => provider switch
    {
        OcrProviderKind.Python => "Python OCR",
        OcrProviderKind.Tesseract => "Tesseract",
        _ => "Windows OCR",
    };
}

/// <summary>Offline OCR using the engine included with Windows (language packs required).</summary>
internal static class WindowsOcrProvider
{
    public static async Task<string> RecognizeAsync(
        Bitmap image,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var engine = CreateEngine(languageCode);

        using var prepared = Prepare(image);
        using var memory = new MemoryStream();
        prepared.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var bytes = memory.ToArray();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        cancellationToken.ThrowIfCancellationRequested();
        var result = await engine.RecognizeAsync(softwareBitmap);
        cancellationToken.ThrowIfCancellationRequested();

        return string.Join(Environment.NewLine, result.Lines.Select(line => line.Text)).Trim();
    }

    private static OcrEngine CreateEngine(string languageCode)
    {
        if (!string.IsNullOrWhiteSpace(languageCode)
            && !string.Equals(languageCode, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var tag = ToWindowsLanguageTag(languageCode);
            var engine = OcrEngine.TryCreateFromLanguage(new Language(tag));
            if (engine is not null)
            {
                return engine;
            }

            var available = string.Join(
                ", ",
                OcrEngine.AvailableRecognizerLanguages.Select(language => language.LanguageTag));

            throw new InvalidOperationException(
                $"Windows OCR does not have the language '{tag}'. " +
                $"Install it in Windows Settings → Time & language → Language, or choose another OCR language. " +
                $"Available: {(available.Length == 0 ? "none" : available)}.");
        }

        return OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException(
                "Windows OCR is not available. Install an OCR language pack in Windows Settings " +
                "→ Time & language → Language.");
    }

    private static Bitmap Prepare(Bitmap source)
    {
        var maximum = OcrEngine.MaxImageDimension;
        if (maximum <= 0 || (source.Width <= maximum && source.Height <= maximum))
        {
            return (Bitmap)source.Clone();
        }

        var scale = Math.Min((double)maximum / source.Width, (double)maximum / source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        var resized = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(resized);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, 0, 0, width, height);
        return resized;
    }

    private static string ToWindowsLanguageTag(string code) => code.ToLowerInvariant() switch
    {
        "en" => "en-US",
        "de" => "de-DE",
        "fr" => "fr-FR",
        "es" => "es-ES",
        "it" => "it-IT",
        "pt" => "pt-BR",
        "nl" => "nl-NL",
        "pl" => "pl-PL",
        "ru" => "ru-RU",
        "uk" => "uk-UA",
        "tr" => "tr-TR",
        "ja" => "ja-JP",
        "zh" => "zh-Hans-CN",
        "ko" => "ko-KR",
        "ar" => "ar-SA",
        "hi" => "hi-IN",
        _ => code,
    };
}
