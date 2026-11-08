using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace LiteSS;

/// <summary>Shows recognized text and an optional translation, with copy/save actions.</summary>
internal sealed class OcrResultForm : Form
{
    private readonly AppSettings _settings;
    private readonly TextBox _sourceBox = new();
    private readonly TextBox _translationBox = new();
    private readonly ComboBox _targetLanguage = Theme.CreateComboBox();
    private readonly Label _status = new();
    private readonly Button _translateButton;
    private readonly Button _copyTranslationButton;
    private CancellationTokenSource? _translationCts;

    public OcrResultForm(AppSettings settings, string text)
    {
        _settings = settings;

        _translateButton = Theme.CreateButton("Translate", primary: true);
        _translateButton.AutoSize = false;
        _translateButton.Size = new Size(96, 30);
        _translateButton.Padding = new Padding(0);
        _translateButton.Margin = new Padding(6, 0, 6, 0);
        _translateButton.Click += async (_, _) => await TranslateAsync();

        _copyTranslationButton = Theme.CreateButton("Copy translation");
        _copyTranslationButton.AutoSize = false;
        _copyTranslationButton.Size = new Size(132, 30);
        _copyTranslationButton.Padding = new Padding(0);
        _copyTranslationButton.Margin = new Padding(0);
        _copyTranslationButton.Click += (_, _) => CopyTranslation();

        _sourceBox.Multiline = true;
        _sourceBox.Dock = DockStyle.Fill;
        _sourceBox.ScrollBars = ScrollBars.Vertical;
        _sourceBox.BorderStyle = BorderStyle.FixedSingle;
        _sourceBox.BackColor = Theme.Viewer;
        _sourceBox.ForeColor = Theme.Text;
        _sourceBox.Font = new Font("Segoe UI", 9.5f);
        _sourceBox.WordWrap = true;
        _sourceBox.AcceptsReturn = true;
        _sourceBox.Margin = new Padding(0);
        _sourceBox.Text = text;

        _translationBox.Multiline = true;
        _translationBox.Dock = DockStyle.Fill;
        _translationBox.ScrollBars = ScrollBars.Vertical;
        _translationBox.BorderStyle = BorderStyle.FixedSingle;
        _translationBox.BackColor = Theme.Viewer;
        _translationBox.ForeColor = Theme.Text;
        _translationBox.Font = new Font("Segoe UI", 9.5f);
        _translationBox.WordWrap = true;
        _translationBox.AcceptsReturn = true;
        _translationBox.Margin = new Padding(0);

        _targetLanguage.Width = 180;
        _targetLanguage.Margin = new Padding(0, 2, 4, 0);
        foreach (var option in Languages.TranslationTarget)
        {
            _targetLanguage.Items.Add(option);
        }

        _targetLanguage.SelectedItem = Languages.Find(
            Languages.TranslationTarget,
            _settings.TranslationTargetLanguage);
        _targetLanguage.SelectedIndexChanged += (_, _) =>
        {
            if (_targetLanguage.SelectedItem is LanguageOption option)
            {
                _settings.TranslationTargetLanguage = option.Code;
                SettingsStore.Save(_settings);
            }
        };

        _status.AutoSize = false;
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = Theme.MutedText;
        _status.BackColor = Theme.Background;
        _status.Text = $"Recognized {text.Length} characters.";

        SuspendLayout();

        Icon = AppIcon.Load(SystemInformation.IconSize.Width);
        Text = "LiteSS OCR result";
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        KeyPreview = true;
        ClientSize = new Size(820, 640);
        MinimumSize = new Size(620, 460);

        Controls.Add(BuildLayout());

        ResumeLayout(false);
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 46f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 54f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));

        root.Controls.Add(BuildSourceHeader(), 0, 0);
        root.Controls.Add(BuildTextBoxHost(_sourceBox), 0, 1);
        root.Controls.Add(BuildTranslationSection(), 0, 2);
        root.Controls.Add(BuildBottomBar(), 0, 3);
        return root;
    }

    private Control BuildSourceHeader()
    {
        var copy = Theme.CreateButton("Copy text");
        copy.AutoSize = false;
        copy.Size = new Size(92, 30);
        copy.Padding = new Padding(0);
        copy.Margin = new Padding(0, 0, 6, 0);
        copy.Click += (_, _) => CopyText();

        var save = Theme.CreateButton("Save text…");
        save.AutoSize = false;
        save.Size = new Size(100, 30);
        save.Padding = new Padding(0);
        save.Margin = new Padding(0);
        save.Click += (_, _) => SaveText();

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Background,
            Margin = new Padding(0),
        };
        buttons.Controls.Add(copy);
        buttons.Controls.Add(save);

        return CreateHeader("Recognized text", buttons);
    }

    private Control BuildTranslationSection()
    {
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Background,
            Margin = new Padding(0),
        };
        buttons.Controls.Add(_translateButton);
        buttons.Controls.Add(_copyTranslationButton);

        var headerContent = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Background,
            Margin = new Padding(0),
        };
        headerContent.Controls.Add(_targetLanguage);
        headerContent.Controls.Add(buttons);

        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        section.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        section.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        section.Controls.Add(CreateHeader("Translation", headerContent), 0, 0);
        section.Controls.Add(BuildTextBoxHost(_translationBox), 0, 1);
        return section;
    }

    private Control BuildBottomBar()
    {
        var close = Theme.CreateButton("Close");
        close.AutoSize = false;
        close.Size = new Size(90, 30);
        close.Padding = new Padding(0);
        close.Margin = new Padding(0);
        close.Click += (_, _) => Close();

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(18, 6, 18, 6),
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        bar.Controls.Add(_status, 0, 0);
        bar.Controls.Add(close, 1, 0);
        return bar;
    }

    private static Control CreateHeader(string title, Control actions)
    {
        var label = new Label
        {
            Text = title,
            AutoSize = true,
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Margin = new Padding(0, 7, 0, 0),
        };

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Background,
            Margin = new Padding(0),
            Padding = new Padding(18, 6, 18, 2),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        header.Controls.Add(label, 0, 0);
        header.Controls.Add(actions, 2, 0);
        return header;
    }

    private static Control BuildTextBoxHost(Control textBox)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Padding = new Padding(18, 2, 18, 8),
            Margin = new Padding(0),
        };
        host.Controls.Add(textBox);
        return host;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_settings.AutoTranslateOcr && _settings.TranslationProvider != TranslationProviderKind.None)
        {
            await TranslateAsync();
        }
        else if (_settings.TranslationProvider == TranslationProviderKind.None)
        {
            _status.Text = "Choose a translation provider in Settings to translate.";
        }
    }

    private async Task TranslateAsync()
    {
        var text = _sourceBox.Text.Trim();
        if (text.Length == 0)
        {
            _status.Text = "There is no recognized text to translate.";
            return;
        }

        if (_settings.TranslationProvider == TranslationProviderKind.None)
        {
            _status.Text = "Choose a translation provider in Settings to translate.";
            return;
        }

        if (_targetLanguage.SelectedItem is not LanguageOption target)
        {
            _status.Text = "Choose a target language.";
            return;
        }

        _translationCts?.Cancel();
        _translationCts?.Dispose();
        _translationCts = new CancellationTokenSource();
        var token = _translationCts.Token;

        var provider = TranslationService.Create(_settings);
        _translateButton.Enabled = false;
        _copyTranslationButton.Enabled = false;
        _targetLanguage.Enabled = false;
        _status.Text = $"Translating with {provider.DisplayName}…";

        try
        {
            var translated = await provider.TranslateAsync(
                text,
                _settings.TranslationSourceLanguage,
                target.Code,
                token);

            _translationBox.Text = translated;
            _status.Text =
                $"Translated to {target.Name} with {provider.DisplayName}.";
        }
        catch (OperationCanceledException)
        {
            // The window is closing.
        }
        catch (Exception ex)
        {
            _status.Text = "Translation failed.";
            MessageBox.Show(
                this,
                ex.Message,
                "Translation failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
            {
                _translateButton.Enabled = true;
                _copyTranslationButton.Enabled = true;
                _targetLanguage.Enabled = true;
            }
        }
    }

    private void CopyText()
    {
        try
        {
            Clipboard.SetText(_sourceBox.Text);
            _status.Text = "Recognized text copied to the clipboard.";
        }
        catch (ExternalException)
        {
            _status.Text = "The clipboard is busy. Try again.";
        }
    }

    private void CopyTranslation()
    {
        if (string.IsNullOrWhiteSpace(_translationBox.Text))
        {
            _status.Text = "There is no translation to copy yet.";
            return;
        }

        try
        {
            Clipboard.SetText(_translationBox.Text);
            _status.Text = "Translation copied to the clipboard.";
        }
        catch (ExternalException)
        {
            _status.Text = "The clipboard is busy. Try again.";
        }
    }

    private void SaveText()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Save OCR result",
            Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = "txt",
            AddExtension = true,
            FileName = $"OCR_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt",
            InitialDirectory = _settings.SaveFolder,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine(_sourceBox.Text.TrimEnd());

        if (!string.IsNullOrWhiteSpace(_translationBox.Text))
        {
            var target = _targetLanguage.SelectedItem as LanguageOption;
            builder.AppendLine();
            builder.AppendLine($"--- Translation ({(target?.Name ?? _settings.TranslationTargetLanguage)}) ---");
            builder.AppendLine(_translationBox.Text.TrimEnd());
        }

        try
        {
            File.WriteAllText(
                dialog.FileName,
                builder.ToString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            _status.Text = $"Saved to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Could not save the OCR result",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _status.Text = "Save failed.";
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.ApplyDarkTitleBar(this);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _translationCts?.Cancel();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _translationCts?.Cancel();
            _translationCts?.Dispose();
        }

        base.Dispose(disposing);
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
