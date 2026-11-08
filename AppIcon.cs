using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace LiteSS;

internal static class AppIcon
{
    private const string ResourceName = "LiteSS.LSS.ico";

    /// <summary>Loads the embedded icon at the requested size (falls back to the executable icon).</summary>
    public static Icon Load(int size)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is not null)
            {
                return new Icon(stream, size, size);
            }
        }
        catch (ArgumentException)
        {
            // Fall through to the executable icon below.
        }

        var executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (executableIcon is not null)
        {
            using (executableIcon)
            {
                return new Icon(executableIcon, size, size);
            }
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
