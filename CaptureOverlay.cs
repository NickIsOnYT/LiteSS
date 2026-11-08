using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace LiteSS;

/// <summary>
/// Full-screen selection overlay. The desktop is frozen into one image first, so the
/// highlight and the final crop are consistent even while the overlay is drawn.
/// </summary>
public sealed class CaptureOverlay : Form
{
    private readonly Bitmap _screen;
    private Point _anchor;
    private Rectangle _selection;
    private bool _dragging;

    public Bitmap? SelectedImage { get; private set; }

    public CaptureOverlay(Bitmap screen)
    {
        _screen = screen;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        Text = "Select an area";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.DrawImageUnscaled(_screen, 0, 0);

        using (var dim = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
        {
            graphics.FillRectangle(dim, ClientRectangle);
        }

        if (_selection.Width > 0 && _selection.Height > 0)
        {
            graphics.DrawImage(_screen, _selection, _selection, GraphicsUnit.Pixel);

            using var outline = new Pen(Theme.Accent);
            graphics.DrawRectangle(
                outline,
                _selection.X,
                _selection.Y,
                _selection.Width - 1,
                _selection.Height - 1);

            DrawSelectionSize(graphics);
        }

        if (!_dragging)
        {
            DrawHint(graphics);
        }

        base.OnPaint(e);
    }

    private void DrawSelectionSize(Graphics graphics)
    {
        var text = $"{_selection.Width} × {_selection.Height}";
        var textSize = graphics.MeasureString(text, Font);
        var boxWidth = textSize.Width + 16f;
        var boxHeight = textSize.Height + 10f;

        var x = (float)_selection.Left;
        var y = _selection.Top - boxHeight - 8f;
        if (y < 8f)
        {
            y = _selection.Bottom + 8f;
        }

        x = Math.Clamp(x, 8f, Math.Max(8f, ClientSize.Width - boxWidth - 8f));
        y = Math.Clamp(y, 8f, Math.Max(8f, ClientSize.Height - boxHeight - 8f));

        using var background = new SolidBrush(Color.FromArgb(235, 28, 28, 28));
        graphics.FillRectangle(background, x, y, boxWidth, boxHeight);
        graphics.DrawString(text, Font, Brushes.White, x + 8f, y + 5f);
    }

    private void DrawHint(Graphics graphics)
    {
        const string hint = "Drag to select an area  •  Esc or right-click to cancel";
        var textSize = graphics.MeasureString(hint, Font);

        var x = (ClientSize.Width - textSize.Width) / 2f;
        const float y = 24f;

        using var background = new SolidBrush(Color.FromArgb(235, 28, 28, 28));
        graphics.FillRectangle(background, x - 12f, y - 8f, textSize.Width + 24f, textSize.Height + 16f);
        graphics.DrawString(hint, Font, Brushes.White, x, y);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            _anchor = e.Location;
            _selection = Rectangle.Empty;
            Invalidate();
        }
        else if (e.Button == MouseButtons.Right)
        {
            CancelSelection();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging)
        {
            var dragged = Rectangle.FromLTRB(
                Math.Min(_anchor.X, e.X),
                Math.Min(_anchor.Y, e.Y),
                Math.Max(_anchor.X, e.X),
                Math.Max(_anchor.Y, e.Y));

            _selection = Rectangle.Intersect(dragged, ClientRectangle);
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dragging && e.Button == MouseButtons.Left)
        {
            _dragging = false;

            if (_selection.Width >= 3 && _selection.Height >= 3)
            {
                SelectedImage = _screen.Clone(_selection, PixelFormat.Format32bppRgb);
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _selection = Rectangle.Empty;
            Invalidate();
        }

        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            CancelSelection();
            return;
        }

        base.OnKeyDown(e);
    }

    private void CancelSelection()
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
