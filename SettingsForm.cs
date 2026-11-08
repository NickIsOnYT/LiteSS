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
    private readonly CheckBox _autoTranslate = Theme.CreateCheckBox("Translate OCR results automatically");
    private readonly Label _folderLabel = new();
    private readonly Button _hotkeyButton;
    private readonly Label _hotkeyStatus = new();

    private readonly ComboBox _ocrProvider = Theme.CreateComboBox();
    private readonly ComboBox _ocrLanguage = Theme.CreateComboBox();
    private readonly TextBox _tessdataFolder = Theme.CreateTextBox();
    private readonly TextBox _pythonExecutable = Theme.CreateTextBox();
    private readonly TextBox _pythonScript = Theme.CreateTextBox();
    private readonly ComboBox _translationProvider = Theme.CreateComboBox();
    private readonly ComboBox _translationSource = Theme.CreateComboBox();
    private readonly ComboBox _translationTarget = Theme.CreateComboBox();
    private readonly TextBox _libreUrl = Theme.CreateTextBox();
    private readonly TextBox _deepLKey = Theme.CreateTextBox(password: true);
    private readonly TextBox _libreKey = Theme.CreateTextBox(password: true);

    private Panel _tessdataRow = null!;
    private Panel _pythonExecutableRow = null!;
    private Panel _pythonScriptRow = null!;
    private Panel _libreUrlRow = null!;
    private Panel _deepLKeyRow = null!;
    private Panel _libreKeyRow = null!;
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
        ClientSize = new Size(520, 620);

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
                     _copyAfterCapture, _autoSave, _notify, _startWithWindows, _startInTray, _autoTranslate,
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
        content.Controls.Add(CreateSectionHeader("OCR"));
        content.Controls.Add(CreateFieldRow("Provider", _ocrProvider));
        content.Controls.Add(CreateFieldRow("Language", _ocrLanguage));
        content.Controls.Add(CreateTesseractRows());
        content.Controls.Add(CreatePythonRows());
        content.Controls.Add(CreateHint(
            "Windows OCR uses installed Windows language packs. Tesseract works out of the box " +
            "in English; other languages download on first use. Python OCR needs pytesseract."));
        content.Controls.Add(CreateSeparator());
        content.Controls.Add(CreateSectionHeader("Translation"));
        content.Controls.Add(CreateFieldRow("Provider", _translationProvider));
        content.Controls.Add(CreateFieldRow("From", _translationSource));
        content.Controls.Add(CreateFieldRow("To", _translationTarget));
        content.Controls.Add(CreateTranslationRows());
        content.Controls.Add(_autoTranslate);
        content.Controls.Add(CreateHint(
            "API keys are encrypted for your Windows account. Argos Translate runs on-device " +
            "through Python; Google, LibreTranslate and DeepL use the network."));
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

    private Control CreateTesseractRows()
    {
        _tessdataFolder.PlaceholderText = "Empty = %LOCALAPPDATA%\\LiteSS\\tessdata";
        _tessdataRow = CreateBrowseRow("Data folder", _tessdataFolder, BrowseForTessdataFolder);
        return _tessdataRow;
    }

    private Control CreatePythonRows()
    {
        _pythonExecutable.PlaceholderText = "python (auto-detect)";
        _pythonScript.PlaceholderText = "Empty = built-in helper";

        var rows = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        _pythonExecutableRow = CreateBrowseRow("Python exe", _pythonExecutable, BrowseForPythonExecutable);
        _pythonScriptRow = CreateBrowseRow("Python script", _pythonScript, BrowseForPythonScript);
        rows.Controls.Add(_pythonExecutableRow);
        rows.Controls.Add(_pythonScriptRow);
        return rows;
    }

    private Control CreateTranslationRows()
    {
        var rows = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        _libreUrl.PlaceholderText = "https://libretranslate.com";
        _libreUrlRow = CreateFieldRow("Server URL", _libreUrl, 360);
        _deepLKey.PlaceholderText = "DeepL API key";
        _deepLKeyRow = CreateFieldRow("API key", _deepLKey, 360);
        _libreKey.PlaceholderText = "Optional API key";
        _libreKeyRow = CreateFieldRow("API key", _libreKey, 360);

        rows.Controls.Add(_libreUrlRow);
        rows.Controls.Add(_deepLKeyRow);
        rows.Controls.Add(_libreKeyRow);
        return rows;
    }

    private void WireSettings()
    {
        PopulateChoiceControls();

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

        WireOcrSettings();
        WireTranslationSettings();
        UpdateOcrRows();
        UpdateTranslationRows();
        UpdateHotkeyUi();
    }

    private void PopulateChoiceControls()
    {
        _ocrProvider.Items.Add(new ProviderItem<OcrProviderKind>(OcrProviderKind.Windows, "Windows OCR"));
        _ocrProvider.Items.Add(new ProviderItem<OcrProviderKind>(OcrProviderKind.Tesseract, "Tesseract (built-in)"));
        _ocrProvider.Items.Add(new ProviderItem<OcrProviderKind>(OcrProviderKind.Python, "Python (Tesseract)"));
        SelectByValue(_ocrProvider, _settings.OcrProvider);

        foreach (var option in Languages.Ocr)
        {
            _ocrLanguage.Items.Add(option);
        }

        _ocrLanguage.SelectedItem = Languages.Find(Languages.Ocr, _settings.OcrLanguage);

        _translationProvider.Items.Add(
            new ProviderItem<TranslationProviderKind>(TranslationProviderKind.None, "None"));
        _translationProvider.Items.Add(
            new ProviderItem<TranslationProviderKind>(TranslationProviderKind.Google, "Google Translate (free)"));
        _translationProvider.Items.Add(
            new ProviderItem<TranslationProviderKind>(TranslationProviderKind.LibreTranslate, "LibreTranslate (server)"));
        _translationProvider.Items.Add(
            new ProviderItem<TranslationProviderKind>(TranslationProviderKind.DeepL, "DeepL (API key)"));
        _translationProvider.Items.Add(
            new ProviderItem<TranslationProviderKind>(TranslationProviderKind.PythonArgos, "Python: Argos Translate"));
        _translationProvider.Items.Add(
            new ProviderItem<TranslationProviderKind>(TranslationProviderKind.PythonLibreTranslate, "Python: LibreTranslate"));
        SelectByValue(_translationProvider, _settings.TranslationProvider);

        foreach (var option in Languages.TranslationSource)
        {
            _translationSource.Items.Add(option);
        }

        _translationSource.SelectedItem = Languages.Find(
            Languages.TranslationSource,
            _settings.TranslationSourceLanguage);

        foreach (var option in Languages.TranslationTarget)
        {
            _translationTarget.Items.Add(option);
        }

        _translationTarget.SelectedItem = Languages.Find(
            Languages.TranslationTarget,
            _settings.TranslationTargetLanguage);

        _tessdataFolder.Text = _settings.TessdataFolder;
        _pythonExecutable.Text = _settings.PythonExecutable;
        _pythonScript.Text = _settings.PythonScriptPath;
        _libreUrl.Text = _settings.LibreTranslateUrl;
        _deepLKey.Text = _settings.DeepLApiKey;
        _libreKey.Text = _settings.LibreTranslateApiKey;
        _autoTranslate.Checked = _settings.AutoTranslateOcr;
    }

    private void WireOcrSettings()
    {
        _ocrProvider.SelectedIndexChanged += (_, _) =>
        {
            _settings.OcrProvider = SelectedOcrProvider();
            SettingsStore.Save(_settings);
            UpdateOcrRows();
        };

        _ocrLanguage.SelectedIndexChanged += (_, _) =>
        {
            if (_ocrLanguage.SelectedItem is LanguageOption option)
            {
                _settings.OcrLanguage = option.Code;
                SettingsStore.Save(_settings);
            }
        };

        _tessdataFolder.TextChanged += (_, _) =>
        {
            _settings.TessdataFolder = _tessdataFolder.Text.Trim();
            SettingsStore.Save(_settings);
        };

        _pythonExecutable.TextChanged += (_, _) =>
        {
            _settings.PythonExecutable = _pythonExecutable.Text.Trim();
            SettingsStore.Save(_settings);
        };

        _pythonScript.TextChanged += (_, _) =>
        {
            _settings.PythonScriptPath = _pythonScript.Text.Trim();
            SettingsStore.Save(_settings);
        };
    }

    private void WireTranslationSettings()
    {
        _translationProvider.SelectedIndexChanged += (_, _) =>
        {
            _settings.TranslationProvider = SelectedTranslationProvider();
            SettingsStore.Save(_settings);
            UpdateTranslationRows();
        };

        _translationSource.SelectedIndexChanged += (_, _) =>
        {
            if (_translationSource.SelectedItem is LanguageOption option)
            {
                _settings.TranslationSourceLanguage = option.Code;
                SettingsStore.Save(_settings);
            }
        };

        _translationTarget.SelectedIndexChanged += (_, _) =>
        {
            if (_translationTarget.SelectedItem is LanguageOption option)
            {
                _settings.TranslationTargetLanguage = option.Code;
                SettingsStore.Save(_settings);
            }
        };

        _libreUrl.TextChanged += (_, _) =>
        {
            _settings.LibreTranslateUrl = _libreUrl.Text.Trim();
            SettingsStore.Save(_settings);
        };

        _deepLKey.TextChanged += (_, _) =>
        {
            _settings.DeepLApiKey = _deepLKey.Text.Trim();
            SettingsStore.Save(_settings);
        };

        _libreKey.TextChanged += (_, _) =>
        {
            _settings.LibreTranslateApiKey = _libreKey.Text.Trim();
            SettingsStore.Save(_settings);
        };

        _autoTranslate.CheckedChanged += (_, _) =>
        {
            _settings.AutoTranslateOcr = _autoTranslate.Checked;
            SettingsStore.Save(_settings);
        };
    }

    private void UpdateOcrRows()
    {
        var provider = SelectedOcrProvider();
        _tessdataRow.Visible = provider == OcrProviderKind.Tesseract;
        _pythonExecutableRow.Visible = provider == OcrProviderKind.Python;
        _pythonScriptRow.Visible = provider == OcrProviderKind.Python;
    }

    private void UpdateTranslationRows()
    {
        var provider = SelectedTranslationProvider();
        var libre = provider is TranslationProviderKind.LibreTranslate
            or TranslationProviderKind.PythonLibreTranslate;

        _libreUrlRow.Visible = libre;
        _libreKeyRow.Visible = libre;
        _deepLKeyRow.Visible = provider == TranslationProviderKind.DeepL;
        _autoTranslate.Enabled = provider != TranslationProviderKind.None;
    }

    private OcrProviderKind SelectedOcrProvider() =>
        _ocrProvider.SelectedItem is ProviderItem<OcrProviderKind> item
            ? item.Value
            : OcrProviderKind.Windows;

    private TranslationProviderKind SelectedTranslationProvider() =>
        _translationProvider.SelectedItem is ProviderItem<TranslationProviderKind> item
            ? item.Value
            : TranslationProviderKind.None;

    private static void SelectByValue<T>(ComboBox combo, T value)
    {
        foreach (var item in combo.Items)
        {
            if (item is ProviderItem<T> provider
                && EqualityComparer<T>.Default.Equals(provider.Value, value))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        combo.SelectedIndex = 0;
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

    private void BrowseForTessdataFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the Tesseract tessdata folder",
            SelectedPath = Directory.Exists(_tessdataFolder.Text) ? _tessdataFolder.Text : string.Empty,
            ShowNewFolderButton = false,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _tessdataFolder.Text = dialog.SelectedPath;
        }
    }

    private void BrowseForPythonExecutable()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose python.exe",
            Filter = "Python (python.exe)|python.exe|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pythonExecutable.Text = dialog.FileName;
        }
    }

    private void BrowseForPythonScript()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose the Python helper script",
            Filter = "Python script (*.py)|*.py|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pythonScript.Text = dialog.FileName;
        }
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

    private static Panel CreateFieldRow(string labelText, Control input, int inputWidth = 300)
    {
        var row = new Panel
        {
            Width = 460,
            Height = 30,
            BackColor = Theme.Background,
            Margin = new Padding(0, 2, 0, 2),
        };

        var label = new Label
        {
            Text = labelText,
            AutoSize = false,
            Width = 118,
            Height = 23,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            Location = new Point(0, 3),
        };

        input.Location = new Point(122, 2);
        input.Width = inputWidth;

        row.Controls.Add(label);
        row.Controls.Add(input);
        return row;
    }

    private Panel CreateBrowseRow(string labelText, TextBox input, Action browse)
    {
        var button = Theme.CreateButton("Browse…");
        button.AutoSize = false;
        button.Size = new Size(82, 27);
        button.Padding = new Padding(0);
        button.Margin = new Padding(0);
        button.Click += (_, _) => browse();

        var row = CreateFieldRow(labelText, input, 246);
        button.Location = new Point(376, 1);
        row.Controls.Add(button);
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
        var hint = new Label
        {
            Text = text,
            AutoSize = false,
            Width = 460,
            Height = 34,
            BackColor = Theme.Background,
            ForeColor = Theme.MutedText,
            Margin = new Padding(0, 2, 0, 6),
        };
        return hint;
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

    private sealed record ProviderItem<T>(T Value, string Text)
    {
        public override string ToString() => Text;
    }
}
