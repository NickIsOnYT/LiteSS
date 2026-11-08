using System.Drawing;
using System.Windows.Forms;

namespace LiteSS;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Func<bool> _isHotkeyRegistered;
    private readonly Func<bool, (bool Success, string? Message)> _setHotkey;
    private readonly CheckBox _copyAfterCapture = Theme.CreateCheckBox("Copy to clipboard");
    private readonly CheckBox _autoSave = Theme.CreateCheckBox("Save to folder");
    private readonly CheckBox _notify = Theme.CreateCheckBox("Show notifications");
    private readonly CheckBox _startWithWindows = Theme.CreateCheckBox("Launch at startup");
    private readonly CheckBox _startInTray = Theme.CreateCheckBox("Start in tray");
    private readonly Label _folderLabel = new();
    private readonly Button _hotkeyButton;
    private readonly Label _hotkeyStatus = new();
    private bool _updatingStartup;

    public SettingsForm(
        AppSettings settings,
        Func<bool> isHotkeyRegistered,
        Func<bool, (bool Success, string? Message)> setHotkey)
    {
        _settings = settings;
        _isHotkeyRegistered = isHotkeyRegistered;
        _setHotkey = setHotkey;

        _hotkeyButton = Theme.CreateButton("Register PrintScreen");
        _hotkeyButton.AutoSize = false;
        _hotkeyButton.Size = new Size(190, 30);
        _hotkeyButton.Padding = new Padding(0);
        _hotkeyButton.Margin = new Padding(0);
        _hotkeyButton.Click += (_, _) => ToggleHotkey();

        SuspendLayout();

        Icon = AppIcon.Load(SystemInformation.IconSize.Width);
        Text = "LiteSS Settings";
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        ClientSize = new Size(520, 490);

        Controls.Add(BuildLayout());

        ResumeLayout(false);
    }

    private Control BuildLayout()
    {
        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Background,
            Padding = new Padding(18, 14, 18, 0),
            Margin = new Padding(0),
        };

        foreach (var checkBox in new[]
                 {
                     _copyAfterCapture, _autoSave, _notify, _startWithWindows, _startInTray,
                 })
        {
            checkBox.Margin = new Padding(0, 4, 0, 4);
        }

        content.Controls.Add(CreateSectionHeader("After capture", first: true));
        content.Controls.Add(_copyAfterCapture);
        content.Controls.Add(_autoSave);
        content.Controls.Add(CreateFolderRow());
        content.Controls.Add(_notify);
        content.Controls.Add(CreateSeparator());
        content.Controls.Add(CreateSectionHeader("Startup"));
        content.Controls.Add(_startWithWindows);
        content.Controls.Add(_startInTray);
        content.Controls.Add(CreateSeparator());
        content.Controls.Add(CreateSectionHeader("Shortcut"));
        content.Controls.Add(CreateHotkeyRow());
        content.Controls.Add(CreateHint("PrintScreen starts a region capture from anywhere."));
        content.Controls.Add(CreatePathHint());

        WireSettings();

        var close = Theme.CreateButton("Close");
        close.AutoSize = false;
        close.Size = new Size(90, 30);
        close.Padding = new Padding(0);
        close.Margin = new Padding(0);
        close.Click += (_, _) => Close();

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Theme.Background,
            Padding = new Padding(18, 8, 18, 0),
            Margin = new Padding(0),
        };
        bottom.Controls.Add(close);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        root.Controls.Add(content, 0, 0);
        root.Controls.Add(bottom, 0, 1);
        return root;
    }

    private void WireSettings()
    {
        _copyAfterCapture.Checked = _settings.CopyAfterCapture;
        _copyAfterCapture.CheckedChanged += (_, _) =>
        {
            _settings.CopyAfterCapture = _copyAfterCapture.Checked;
            SettingsStore.Save(_settings);
        };

        _autoSave.Checked = _settings.AutoSaveAfterCapture;
        _autoSave.CheckedChanged += (_, _) =>
        {
            _settings.AutoSaveAfterCapture = _autoSave.Checked;
            SettingsStore.Save(_settings);
        };

        _notify.Checked = _settings.ShowNotifications;
        _notify.CheckedChanged += (_, _) =>
        {
            _settings.ShowNotifications = _notify.Checked;
            SettingsStore.Save(_settings);
        };

        _startWithWindows.Checked = StartupRegistration.IsEnabled();
        _startWithWindows.CheckedChanged += OnStartWithWindowsChanged;

        _startInTray.Checked = _settings.StartInTray;
        _startInTray.CheckedChanged += (_, _) =>
        {
            _settings.StartInTray = _startInTray.Checked;
            SettingsStore.Save(_settings);

            if (StartupRegistration.IsEnabled())
            {
                StartupRegistration.Apply(true, _settings.StartInTray, out _);
            }
        };

        UpdateHotkeyUi();
    }

    private void OnStartWithWindowsChanged(object? sender, EventArgs e)
    {
        if (_updatingStartup)
        {
            return;
        }

        var enabled = _startWithWindows.Checked;
        if (StartupRegistration.Apply(enabled, _settings.StartInTray, out var error))
        {
            return;
        }

        _updatingStartup = true;
        _startWithWindows.Checked = !enabled;
        _updatingStartup = false;

        MessageBox.Show(
            this,
            error,
            "Could not update the startup setting",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void BrowseForFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the folder for automatic screenshot saves",
            SelectedPath = _settings.SaveFolder,
            ShowNewFolderButton = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.SaveFolder = dialog.SelectedPath;
        _folderLabel.Text = dialog.SelectedPath;
        _settings.AutoSaveAfterCapture = true;
        _autoSave.Checked = true;
        SettingsStore.Save(_settings);
    }

    private void ToggleHotkey()
    {
        var registering = !_isHotkeyRegistered();
        var (success, message) = _setHotkey(registering);

        if (!success)
        {
            var text = string.IsNullOrWhiteSpace(message)
                ? "PrintScreen could not be registered. It may already be in use by another app."
                : $"PrintScreen could not be registered. {message}";

            MessageBox.Show(this, text, "PrintScreen shortcut", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        UpdateHotkeyUi();
    }

    private void UpdateHotkeyUi()
    {
        var registered = _isHotkeyRegistered();
        _hotkeyButton.Text = registered ? "Unregister PrintScreen" : "Register PrintScreen";
        _hotkeyStatus.Text = registered ? "Registered" : "Not registered";
    }

    private Control CreateFolderRow()
    {
        var row = new Panel
        {
            Width = 460,
            Height = 28,
            BackColor = Theme.Background,
            Margin = new Padding(0, 2, 0, 8),
        };

        var frame = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(340, 27),
            BackColor = Theme.Border,
            Padding = new Padding(1),
        };

        _folderLabel.Dock = DockStyle.Fill;
        _folderLabel.AutoEllipsis = true;
        _folderLabel.TextAlign = ContentAlignment.MiddleLeft;
        _folderLabel.BackColor = Theme.Viewer;
        _folderLabel.ForeColor = Theme.Text;
        _folderLabel.Padding = new Padding(6, 0, 6, 0);
        _folderLabel.Text = _settings.SaveFolder;
        frame.Controls.Add(_folderLabel);

        var browse = Theme.CreateButton("Browse…");
        browse.AutoSize = false;
        browse.Size = new Size(86, 27);
        browse.Padding = new Padding(0);
        browse.Margin = new Padding(0);
        browse.Location = new Point(350, 0);
        browse.Click += (_, _) => BrowseForFolder();

        row.Controls.Add(frame);
        row.Controls.Add(browse);
        return row;
    }

    private Control CreateHotkeyRow()
    {
        var row = new Panel
        {
            Width = 460,
            Height = 32,
            BackColor = Theme.Background,
            Margin = new Padding(0, 0, 0, 4),
        };

        _hotkeyButton.Location = new Point(0, 0);

        _hotkeyStatus.AutoSize = true;
        _hotkeyStatus.BackColor = Theme.Background;
        _hotkeyStatus.ForeColor = Theme.MutedText;
        _hotkeyStatus.Location = new Point(204, 7);

        row.Controls.Add(_hotkeyButton);
        row.Controls.Add(_hotkeyStatus);
        return row;
    }

    private static Label CreateSectionHeader(string text, bool first = false)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Margin = new Padding(0, first ? 0 : 10, 0, 6),
        };
    }

    private static Label CreateHint(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            BackColor = Theme.Background,
            ForeColor = Theme.MutedText,
            Margin = new Padding(0, 0, 0, 6),
        };
    }

    private static Label CreatePathHint()
    {
        return new Label
        {
            Text = $"Settings folder: {SettingsStore.SettingsFolder}",
            AutoSize = false,
            Width = 460,
            Height = 16,
            AutoEllipsis = true,
            BackColor = Theme.Background,
            ForeColor = Theme.MutedText,
            Margin = new Padding(0, 8, 0, 0),
        };
    }

    private static Panel CreateSeparator()
    {
        return new Panel
        {
            Width = 460,
            Height = 1,
            BackColor = Theme.Border,
            Margin = new Padding(0, 10, 0, 10),
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.ApplyDarkTitleBar(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            return;
        }

        base.OnKeyDown(e);
    }
}
