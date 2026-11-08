using System.Windows.Forms;
using Microsoft.Win32;

namespace LiteSS;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LiteSS";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Keeps the Run entry pointing at the executable that is running now.</summary>
    public static void RefreshIfEnabled(bool startInTray)
    {
        if (IsEnabled())
        {
            Apply(true, startInTray, out _);
        }
    }

    public static bool Apply(bool enabled, bool startInTray, out string error)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                error = "The startup registry key could not be opened.";
                return false;
            }

            if (enabled)
            {
                var command = $"\"{Application.ExecutablePath}\"";
                if (startInTray)
                {
                    command += " --tray";
                }

                key.SetValue(ValueName, command, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
