using System.Text.Json;

namespace LiteSS;

internal sealed class AppSettings
{
    public bool CopyAfterCapture { get; set; } = true;
    public bool AutoSaveAfterCapture { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public bool StartInTray { get; set; }
    public bool PrintScreenHotkey { get; set; }
    public string SaveFolder { get; set; } = string.Empty;
}

internal static class SettingsStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LiteSS");

    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (settings is not null)
                {
                    if (string.IsNullOrWhiteSpace(settings.SaveFolder))
                    {
                        settings.SaveFolder = DefaultSaveFolder;
                    }

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
}
