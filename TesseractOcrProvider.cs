using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Reflection;
using Tesseract;

namespace LiteSS;

/// <summary>
/// Tesseract OCR through the Tesseract NuGet package. The native x64 libraries are
/// embedded in LiteSS and extracted on first use so the single-file builds stay single-file.
/// </summary>
internal static class TesseractOcrProvider
{
    private const string LeptonicaResource = "LiteSS.native.leptonica-1.82.0.dll";
    private const string TesseractResource = "LiteSS.native.tesseract50.dll";
    private const string EnglishDataResource = "LiteSS.tessdata.eng.traineddata";

    private static readonly object NativeLock = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static bool _nativeReady;

    public static async Task<string> RecognizeAsync(
        Bitmap image,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var language = ToTesseractLanguage(settings.OcrLanguage);
        var dataFolder = await EnsureLanguageDataAsync(settings, language, cancellationToken);

        EnsureNativeLibraries();

        using var memory = new MemoryStream();
        image.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
        memory.Position = 0;
        var png = memory.ToArray();

        return await Task.Run(
            () =>
            {
                var dataPath = dataFolder;

                try
                {
                    using var engine = new TesseractEngine(dataPath, language, EngineMode.Default);
                    using var pix = Pix.LoadFromMemory(png);
                    using var page = engine.Process(pix, PageSegMode.Auto);
                    return page.GetText().Trim();
                }
                catch (TesseractException ex)
                {
                    throw new InvalidOperationException(
                        $"Tesseract could not start with the language data in '{dataFolder}'. " +
                        $"Check that the folder contains '{language}.traineddata'. {ex.Message}",
                        ex);
                }
                catch (DllNotFoundException ex)
                {
                    throw new InvalidOperationException(
                        "The Tesseract native libraries could not be loaded. " +
                        "The 64-bit Microsoft Visual C++ 2015-2022 Redistributable may be missing.",
                        ex);
                }
            },
            cancellationToken);
    }

    private static void EnsureNativeLibraries()
    {
        lock (NativeLock)
        {
            if (_nativeReady)
            {
                return;
            }

            if (!Environment.Is64BitProcess)
            {
                throw new PlatformNotSupportedException(
                    "The built-in Tesseract engine is 64-bit only. Use Windows OCR or Python OCR in a 32-bit build.");
            }

            var root = Path.Combine(SettingsStore.SettingsFolder, "tesseract");
            var x64 = Path.Combine(root, "x64");
            Directory.CreateDirectory(x64);

            Extract(LeptonicaResource, Path.Combine(x64, "leptonica-1.82.0.dll"));
            Extract(TesseractResource, Path.Combine(x64, "tesseract50.dll"));

            // The Tesseract wrapper looks for <CustomSearchPath>\x64\<native dll>.
            InteropDotNet.LibraryLoader.Instance.CustomSearchPath = root;
            _nativeReady = true;
        }
    }

    private static async Task<string> EnsureLanguageDataAsync(
        AppSettings settings,
        string language,
        CancellationToken cancellationToken)
    {
        var existing = FindExistingLanguageData(settings, language);
        if (existing is not null)
        {
            return existing;
        }

        var folder = ResolveWritableTessdataFolder(settings);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, language + ".traineddata");

        if (language == "eng")
        {
            ExtractEnglishData(path);
        }
        else
        {
            await DownloadLanguageDataAsync(language, path, cancellationToken);
        }

        return folder;
    }

    private static string? FindExistingLanguageData(AppSettings settings, string language)
    {
        foreach (var candidate in CandidateFolders(settings))
        {
            var folder = NormalizeTessdataFolder(candidate);
            var file = Path.Combine(folder, language + ".traineddata");
            if (File.Exists(file))
            {
                return folder;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateFolders(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.TessdataFolder))
        {
            yield return settings.TessdataFolder;
        }

        yield return Path.Combine(SettingsStore.SettingsFolder, "tessdata");

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "Tesseract-OCR");
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "Tesseract-OCR");
        }

        var localPrograms = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Tesseract-OCR");
        yield return localPrograms;
    }

    private static string ResolveWritableTessdataFolder(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.TessdataFolder))
        {
            var configured = NormalizeTessdataFolder(settings.TessdataFolder);
            if (IsWritable(configured))
            {
                return configured;
            }
        }

        return Path.Combine(SettingsStore.SettingsFolder, "tessdata");
    }

    private static string NormalizeTessdataFolder(string folder)
    {
        var expanded = Environment.ExpandEnvironmentVariables(folder.Trim().Trim('"'));
        if (expanded.EndsWith("tessdata", StringComparison.OrdinalIgnoreCase))
        {
            return expanded;
        }

        var nested = Path.Combine(expanded, "tessdata");
        return Directory.Exists(nested) ? nested : expanded;
    }

    private static bool IsWritable(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, ".litess-write-test");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void ExtractEnglishData(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EnglishDataResource)
            ?? throw new InvalidOperationException("The bundled English Tesseract data is missing from this build.");

        var temporary = path + ".tmp";
        using (var file = File.Create(temporary))
        {
            stream.CopyTo(file);
        }

        File.Move(temporary, path, overwrite: true);
    }

    private static async Task DownloadLanguageDataAsync(
        string language,
        string path,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/{language}.traineddata";

        using var response = await Http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Tesseract language data for '{language}' was not found locally and could not be " +
                $"downloaded (HTTP {(int)response.StatusCode}). Put {language}.traineddata in " +
                $"'{Path.GetDirectoryName(path)}', or use Windows OCR / Python OCR.");
        }

        var temporary = path + ".tmp";
        try
        {
            await using (var file = File.Create(temporary))
            {
                await response.Content.CopyToAsync(file, cancellationToken);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception)
            {
                // Best effort.
            }

            throw;
        }
    }

    private static void Extract(string resourceName, string path)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded native library '{resourceName}' is missing.");

        if (File.Exists(path) && new FileInfo(path).Length == stream.Length)
        {
            return;
        }

        var temporary = path + ".tmp";
        using (var file = File.Create(temporary))
        {
            stream.CopyTo(file);
        }

        File.Move(temporary, path, overwrite: true);
    }

    private static string ToTesseractLanguage(string code) => code.ToLowerInvariant() switch
    {
        "auto" => "eng",
        "en" => "eng",
        "de" => "deu",
        "fr" => "fra",
        "es" => "spa",
        "it" => "ita",
        "pt" => "por",
        "nl" => "nld",
        "pl" => "pol",
        "ru" => "rus",
        "uk" => "ukr",
        "tr" => "tur",
        "ja" => "jpn",
        "zh" => "chi_sim",
        "ko" => "kor",
        "ar" => "ara",
        "hi" => "hin",
        _ => "eng",
    };
}
