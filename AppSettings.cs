using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiteSS;

internal sealed class AppSettings
{
    public bool CopyAfterCapture { get; set; } = true;
    public bool AutoSaveAfterCapture { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public bool StartInTray { get; set; }
    public bool PrintScreenHotkey { get; set; }
    public string SaveFolder { get; set; } = string.Empty;

    public OcrProviderKind OcrProvider { get; set; } = OcrProviderKind.Windows;
    public string OcrLanguage { get; set; } = "auto";
    public string TessdataFolder { get; set; } = string.Empty;
    public string PythonExecutable { get; set; } = string.Empty;
    public string PythonScriptPath { get; set; } = string.Empty;

    public TranslationProviderKind TranslationProvider { get; set; } = TranslationProviderKind.None;
    public string TranslationSourceLanguage { get; set; } = "auto";
    public string TranslationTargetLanguage { get; set; } = "en";
    public string LibreTranslateUrl { get; set; } = "https://libretranslate.com";
    public bool AutoTranslateOcr { get; set; }

    public string DeepLApiKeyEncrypted { get; set; } = string.Empty;
    public string LibreTranslateApiKeyEncrypted { get; set; } = string.Empty;

    [JsonIgnore]
    public string DeepLApiKey
    {
        get => SecretProtector.Unprotect(DeepLApiKeyEncrypted);
        set => DeepLApiKeyEncrypted = SecretProtector.Protect(value);
    }

    [JsonIgnore]
    public string LibreTranslateApiKey
    {
        get => SecretProtector.Unprotect(LibreTranslateApiKeyEncrypted);
        set => LibreTranslateApiKeyEncrypted = SecretProtector.Protect(value);
    }
}

internal static class SettingsStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LiteSS");

    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Folder under %LOCALAPPDATA% that holds the user settings.</summary>
    public static string SettingsFolder => FolderPath;

    public static string SettingsFile => FilePath;

    public static string DefaultSaveFolder
    {
        get
        {
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            return string.IsNullOrEmpty(pictures)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : pictures;
        }
    }

    public static AppSettings Load()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);

            if (File.Exists(FilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
                if (settings is not null)
                {
                    Normalize(settings);
                    return settings;
                }
            }
        }
        catch (Exception)
        {
            // Unreadable settings should never stop the app from starting.
        }

        return new AppSettings { SaveFolder = DefaultSaveFolder };
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception)
        {
            // Settings are a convenience; ignore write failures.
        }
    }

    private static void Normalize(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SaveFolder))
        {
            settings.SaveFolder = DefaultSaveFolder;
        }

        if (string.IsNullOrWhiteSpace(settings.OcrLanguage))
        {
            settings.OcrLanguage = "auto";
        }

        if (string.IsNullOrWhiteSpace(settings.TranslationSourceLanguage))
        {
            settings.TranslationSourceLanguage = "auto";
        }

        if (string.IsNullOrWhiteSpace(settings.TranslationTargetLanguage))
        {
            settings.TranslationTargetLanguage = "en";
        }

        if (string.IsNullOrWhiteSpace(settings.LibreTranslateUrl))
        {
            settings.LibreTranslateUrl = "https://libretranslate.com";
        }
    }
}
