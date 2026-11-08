using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiteSS;

internal static class NativeMethods
{
    public const int WmHotKey = 0x0312;
    public const int WmClose = 0x0010;
    public const int WmQueryEndSession = 0x0011;
    public const uint VkSnapshot = 0x2C;

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Uses the Windows dark title bar so the whole window matches the theme.</summary>
    public static void ApplyDarkTitleBar(Form form)
    {
        if (!form.IsHandleCreated)
        {
            return;
        }

        var enabled = 1;
        var result = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        if (result != 0)
        {
            DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
        }
    }

    public static bool RegisterPrintScreen(IntPtr windowHandle, int id) =>
        RegisterHotKey(windowHandle, id, 0, VkSnapshot);

    public static void UnregisterHotKeyById(IntPtr windowHandle, int id) =>
        UnregisterHotKey(windowHandle, id);
}
