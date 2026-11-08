using System.Windows.Forms;

namespace LiteSS;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var startInTray = args.Any(arg =>
            string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase));

        Application.Run(new MainForm(startInTray));
    }
}
