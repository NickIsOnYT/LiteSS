using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiteSS;

public sealed class MainForm : Form
{
    private const int PrintScreenHotkeyId = 0x4C53;

    private readonly AppSettings _settings;
    private readonly bool _startHidden;
    private readonly PictureBox _preview = new();
    private readonly Label _emptyHint = new();
    private readonly Label _statusLabel = new();
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;
    private Bitmap? _image;
    private bool _capturing;
    private bool _ocrRunning;
    private bool _exitRequested;
    private bool _printScreenRegistered;
    private bool _systemShuttingDown;

    public MainForm(bool startHidden = false)
    {
        _settings = SettingsStore.Load();
        _startHidden = startHidden;

        // Follow whichever build the user is running, so a moved/updated exe keeps starting.
        StartupRegistration.RefreshIfEnabled(_settings.StartInTray);

        SuspendLayout();

        Icon = AppIcon.Load(SystemInformation.IconSize.Width);
        Text = "LiteSS";
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 420);
        Size = new Size(860, 600);
        KeyPreview = true;

        Controls.Add(BuildRoot());

        _trayMenu = BuildTrayMenu();
        _trayIcon = new NotifyIcon
        {
            Icon = AppIcon.Load(SystemInformation.SmallIconSize.Width),
            Text = "LiteSS — screenshot tool",
            Visible = true,
            ContextMenuStrip = _trayMenu,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreWindow();

        ResumeLayout(false);
    }

    private Control BuildRoot()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));

        root.Controls.Add(BuildToolbar(), 0, 0);
        root.Controls.Add(BuildViewer(), 0, 1);
        root.Controls.Add(BuildStatusBar(), 0, 2);
        return root;
    }

    private Control BuildToolbar()
    {
        var toolbar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Padding = new Padding(14, 0, 14, 0),
            Margin = new Padding(0),
        };

        var capture = Theme.CreateButton("Capture Region", primary: true);
        capture.Click += (_, _) => CaptureRegion();

        var fullScreen = Theme.CreateButton("Full Screen");
        fullScreen.Click += (_, _) => CaptureFullScreen();

        var ocr = Theme.CreateButton("OCR");
        ocr.Click += (_, _) => RunOcr();

        var copy = Theme.CreateButton("Copy");
        copy.Click += (_, _) => CopyImage();

        var save = Theme.CreateButton("Save");
        save.Click += (_, _) => SaveImage();

        var settings = Theme.CreateButton("Settings");
        settings.Click += (_, _) => OpenSettings();

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Background,
            Padding = new Padding(0, 12, 0, 0),
            Margin = new Padding(0),
        };
        left.Controls.Add(capture);
        left.Controls.Add(fullScreen);
        left.Controls.Add(ocr);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Background,
            Padding = new Padding(0, 12, 0, 0),
            Margin = new Padding(0),
        };
        right.Controls.Add(copy);
        right.Controls.Add(save);
        right.Controls.Add(settings);

        toolbar.Controls.Add(left);
        toolbar.Controls.Add(right);
        return toolbar;
    }

    private Control BuildViewer()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Padding = new Padding(14, 0, 14, 14),
            Margin = new Padding(0),
        };

        var frame = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Border,
            Padding = new Padding(1),
            Margin = new Padding(0),
        };

        var canvas = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Viewer,
            Margin = new Padding(0),
        };

        _preview.Dock = DockStyle.Fill;
        _preview.BackColor = Theme.Viewer;
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        _preview.Margin = new Padding(0);

        _emptyHint.Dock = DockStyle.Fill;
        _emptyHint.BackColor = Theme.Viewer;
        _emptyHint.ForeColor = Theme.MutedText;
        _emptyHint.TextAlign = ContentAlignment.MiddleCenter;
        _emptyHint.Font = new Font("Segoe UI", 10f);
        _emptyHint.Text = "No capture yet.\r\nUse \"Capture Region\" or \"Full Screen\" to start.";

        canvas.Controls.Add(_preview);
        canvas.Controls.Add(_emptyHint);
        _emptyHint.BringToFront();

        frame.Controls.Add(canvas);
        outer.Controls.Add(frame);
        return outer;
    }

    private Control BuildStatusBar()
    {
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = Theme.MutedText;
        _statusLabel.Padding = new Padding(16, 0, 16, 0);
        _statusLabel.Text = "Ready.";

        var bar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Margin = new Padding(0),
        };
        bar.Controls.Add(_statusLabel);
        return bar;
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = Theme.CreateContextMenu();

        menu.Items.Add(CreateTrayItem("Capture Region", (_, _) => CaptureRegion()));
        menu.Items.Add(CreateTrayItem("Full Screen", (_, _) => CaptureFullScreen()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateTrayItem("Show LiteSS", (_, _) => RestoreWindow()));
        menu.Items.Add(CreateTrayItem("Settings…", (_, _) =>
        {
            RestoreWindow();
            OpenSettings();
        }));
        menu.Items.Add(CreateTrayItem("Exit", (_, _) => ExitApplication()));

        return menu;
    }

    private static ToolStripMenuItem CreateTrayItem(string text, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text) { ForeColor = Theme.Text };
        item.Click += onClick;
        return item;
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings, () => _printScreenRegistered, SetPrintScreenRegistration);
        dialog.ShowDialog(this);
    }

    private void CaptureRegion()
    {
        if (_capturing)
        {
            return;
        }

        var windowWasOpen = Visible && WindowState != FormWindowState.Minimized;
        _capturing = true;
        _trayMenu.Enabled = false;

        Hide();
        try
        {
            // Give the desktop a moment to repaint without the main window.
            Thread.Sleep(160);

            using var screen = ScreenCapture.CaptureVirtualScreen();
            using var overlay = new CaptureOverlay(screen);

            if (overlay.ShowDialog() == DialogResult.OK && overlay.SelectedImage is { } selection)
            {
                ShowImage(selection);
                RunAfterCaptureTasks(windowWasOpen);
            }
        }
        finally
        {
            _capturing = false;
            _trayMenu.Enabled = true;

            if (windowWasOpen)
            {
                RestoreWindow();
            }
        }
    }

    private void CaptureFullScreen()
    {
        if (_capturing)
        {
            return;
        }

        var windowWasOpen = Visible && WindowState != FormWindowState.Minimized;
        _capturing = true;
        _trayMenu.Enabled = false;

        Hide();
        try
        {
            Thread.Sleep(160);
            ShowImage(ScreenCapture.CapturePrimaryScreen());
            RunAfterCaptureTasks(windowWasOpen);
        }
        finally
        {
            _capturing = false;
            _trayMenu.Enabled = true;

            if (windowWasOpen)
            {
                RestoreWindow();
            }
        }
    }

    /// <summary>Runs the user's "after capture" actions and reports the result.</summary>
    private void RunAfterCaptureTasks(bool windowWasOpen)
    {
        if (_image is null)
        {
            return;
        }

        var notes = new List<string>();

        if (_settings.CopyAfterCapture)
        {
            try
            {
                Clipboard.SetImage(_image);
                notes.Add("Copied to clipboard");
            }
            catch (ExternalException)
            {
                notes.Add("Clipboard was busy");
            }
        }

        if (_settings.AutoSaveAfterCapture)
        {
            try
            {
                Directory.CreateDirectory(_settings.SaveFolder);
                var path = GetUniqueSavePath(_settings.SaveFolder);
                _image.Save(path, ImageFormat.Png);
                notes.Add($"Saved to {path}");
            }
            catch (Exception ex)
            {
                notes.Add($"Auto-save failed: {ex.Message}");
            }
        }

        if (notes.Count == 0)
        {
            return;
        }

        var summary = string.Join(" · ", notes);

        if (windowWasOpen)
        {
            SetStatus($"{_image.Width} × {_image.Height} px — {summary}");
        }
        else if (_settings.ShowNotifications)
        {
            _trayIcon.ShowBalloonTip(2500, "Screenshot captured", summary, ToolTipIcon.Info);
        }
    }

    /// <summary>Recognizes text in the current capture and opens the result window.</summary>
    private async void RunOcr()
    {
        if (_ocrRunning)
        {
            return;
        }

        if (_image is null)
        {
            SetStatus("Nothing to recognize yet.");
            return;
        }

        _ocrRunning = true;
        var provider = OcrService.DisplayName(_settings.OcrProvider);

        // Work on a copy so a new capture cannot dispose the bitmap mid-recognition.
        Bitmap snapshot;
        try
        {
            snapshot = (Bitmap)_image.Clone();
        }
        catch (Exception ex)
        {
            _ocrRunning = false;
            SetStatus("OCR failed.");
            MessageBox.Show(
                this,
                ex.Message,
                "OCR failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        SetStatus($"Recognizing text with {provider}…");

        try
        {
            using (snapshot)
            {
                var text = await OcrService.RecognizeAsync(snapshot, _settings, CancellationToken.None);

                if (string.IsNullOrWhiteSpace(text))
                {
                    SetStatus("No text was recognized.");
                    MessageBox.Show(
                        this,
                        "No text was recognized in this screenshot.",
                        "OCR",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                SetStatus($"Recognized {text.Length} characters with {provider}.");
                using var result = new OcrResultForm(_settings, text);
                result.ShowDialog(this);
                SetStatus("Ready.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"{provider} failed.");
            MessageBox.Show(
                this,
                ex.Message,
                $"{provider} failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _ocrRunning = false;
        }
    }

    private void CopyImage()
    {
        if (_image is null)
        {
            SetStatus("Nothing to copy yet.");
            return;
        }

        try
        {
            Clipboard.SetImage(_image);
            SetStatus("Copied to clipboard.");
        }
        catch (ExternalException)
        {
            SetStatus("Clipboard is busy. Try again.");
        }
    }

    private void SaveImage()
    {
        if (_image is null)
        {
            SetStatus("Nothing to save yet.");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save screenshot",
            Filter = "PNG image (*.png)|*.png",
            DefaultExt = "png",
            AddExtension = true,
            FileName = $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png",
            InitialDirectory = _settings.SaveFolder,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _image.Save(dialog.FileName, ImageFormat.Png);
            SetStatus($"Saved to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Could not save the screenshot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            SetStatus("Save failed.");
        }
    }

    private static string GetUniqueSavePath(string folder)
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var path = Path.Combine(folder, $"Screenshot_{stamp}.png");
        var suffix = 1;

        while (File.Exists(path))
        {
            path = Path.Combine(folder, $"Screenshot_{stamp}_{suffix++}.png");
        }

        return path;
    }

    private (bool Success, string? Message) SetPrintScreenRegistration(bool register)
    {
        if (register)
        {
            if (_printScreenRegistered)
            {
                return (true, null);
            }

            if (NativeMethods.RegisterPrintScreen(Handle, PrintScreenHotkeyId))
            {
                _printScreenRegistered = true;
                _settings.PrintScreenHotkey = true;
                SettingsStore.Save(_settings);
                return (true, null);
            }

            var error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return (false, error);
        }

        if (_printScreenRegistered)
        {
            NativeMethods.UnregisterHotKeyById(Handle, PrintScreenHotkeyId);
            _printScreenRegistered = false;
        }

        _settings.PrintScreenHotkey = false;
        SettingsStore.Save(_settings);
        return (true, null);
    }

    private void ShowImage(Bitmap image)
    {
        var previous = _image;
        _image = image;
        _preview.Image = _image;
        previous?.Dispose();

        _emptyHint.Visible = false;
        SetStatus($"{image.Width} × {image.Height} px — Ctrl+S to save, Ctrl+C to copy.");
    }

    private void SetStatus(string message) => _statusLabel.Text = message;

    private void RestoreWindow()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Show();
        Activate();
        BringToFront();
    }

    private void HideToTray()
    {
        Hide();
        SetStatus("Running in the system tray.");
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        _trayIcon.Visible = false;
        Close();
    }

    protected override void SetVisibleCore(bool value)
    {
        // Start hidden in the tray (used by the "--tray" startup argument) without flashing the window.
        if (_startHidden && !IsHandleCreated)
        {
            value = false;
            CreateHandle();
        }

        base.SetVisibleCore(value);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exitRequested
            && !_systemShuttingDown
            && e.CloseReason != CloseReason.WindowsShutDown
            && e.CloseReason != CloseReason.TaskManagerClosing
            && e.CloseReason != CloseReason.ApplicationExitCall)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (WindowState == FormWindowState.Minimized && Visible)
        {
            Hide();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.ApplyDarkTitleBar(this);

        _printScreenRegistered = false;
        if (_settings.PrintScreenHotkey)
        {
            _printScreenRegistered = NativeMethods.RegisterPrintScreen(Handle, PrintScreenHotkeyId);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmQueryEndSession)
        {
            _systemShuttingDown = true;
        }

        if (m.Msg == NativeMethods.WmHotKey && m.WParam.ToInt32() == PrintScreenHotkeyId)
        {
            CaptureRegion();
            return;
        }

        // Some tools close windows by posting WM_CLOSE directly, bypassing FormClosing.
        if (m.Msg == NativeMethods.WmClose && !_exitRequested && !_systemShuttingDown)
        {
            HideToTray();
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.N)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            CaptureRegion();
            return;
        }

        if (e.Control && e.KeyCode == Keys.S)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SaveImage();
            return;
        }

        if (e.Control && e.KeyCode == Keys.C)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            CopyImage();
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_printScreenRegistered && IsHandleCreated)
            {
                NativeMethods.UnregisterHotKeyById(Handle, PrintScreenHotkeyId);
                _printScreenRegistered = false;
            }

            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayMenu.Dispose();
            _preview.Image = null;
            _image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
