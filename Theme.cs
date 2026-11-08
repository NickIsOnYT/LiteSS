using System.Drawing;
using System.Windows.Forms;

namespace LiteSS;

/// <summary>Shared colors and simple flat controls for the dark UI.</summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(30, 30, 30);
    public static readonly Color Surface = Color.FromArgb(45, 45, 45);
    public static readonly Color SurfaceHover = Color.FromArgb(60, 60, 60);
    public static readonly Color Border = Color.FromArgb(62, 62, 62);
    public static readonly Color Viewer = Color.FromArgb(22, 22, 22);
    public static readonly Color Text = Color.FromArgb(230, 230, 230);
    public static readonly Color MutedText = Color.FromArgb(150, 150, 150);
    public static readonly Color Accent = Color.FromArgb(0, 120, 212);
    public static readonly Color AccentHover = Color.FromArgb(20, 135, 225);
    public static readonly Color AccentPressed = Color.FromArgb(0, 100, 180);

    public static Button CreateButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 7, 16, 7),
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            BackColor = primary ? Accent : Surface,
            ForeColor = Text,
            Cursor = Cursors.Hand,
            TabStop = false,
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHover : SurfaceHover;
        button.FlatAppearance.MouseDownBackColor = primary ? AccentPressed : Surface;

        return button;
    }

    public static CheckBox CreateCheckBox(string text)
    {
        return new CheckBox
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            BackColor = Background,
            ForeColor = Text,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 5, 14, 0),
        };
    }

    public static ContextMenuStrip CreateContextMenu()
    {
        return new ContextMenuStrip
        {
            BackColor = Surface,
            ForeColor = Text,
            ShowImageMargin = false,
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable()),
        };
    }

    private sealed class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Surface;
        public override Color MenuItemSelected => SurfaceHover;
        public override Color MenuItemSelectedGradientBegin => SurfaceHover;
        public override Color MenuItemSelectedGradientEnd => SurfaceHover;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
    }
}
