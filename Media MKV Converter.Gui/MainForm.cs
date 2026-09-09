using Media_MKV_Converter.Core;
using System.Diagnostics;

namespace Media_MKV_Converter.Gui;

public partial class MainForm : Form
{
    private static readonly Color StartLineColor = Color.FromArgb(220, 235, 255);
    private static readonly Color OutputLineColor = Color.FromArgb(225, 245, 225);
    private static readonly Color SummaryLineColor = Color.FromArgb(183, 225, 183);
    private static readonly Color ErrorLineColor = Color.FromArgb(255, 220, 220);

    private CancellationTokenSource? _runCancellation;
    private int _successCount;
    private int _skipCount;
    private int _failureCount;

    public MainForm()
    {
        InitializeComponent();
        txtInputFolder.Text = Environment.CurrentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        txtMkvMerge.Text = new MediaConverterOptions().MkvMergePath;
        txtMkvPropEdit.Text = new MediaConverterOptions().MkvPropEditPath;
        txtSkip.Text = @"\Processing\";
        chkOverwrite.Checked = true;
        chkTraverseSubfolders.Checked = true;
        chkMaintainFolderStructure.Checked = false;
        SetAdvancedVisibility(false);
        UpdateRunningState(false);
        UpdateSkipDirectoryState();
        ApplyResultsSummaryColors();
        UpdateLogItemHeight();
    }

    private async void btnStart_Click(object? sender, EventArgs e)
    {
        if (_runCancellation is not null)
        {
            return;
        }

        MediaConverterOptions options;
        try
        {
            options = BuildOptions();
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, ex.Message, "Invalid Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        lstLog.Items.Clear();
        progressOverall.Value = 0;
        lblProgressPercent.Text = "0%";
        lblSummary.Text = $"Directory: {options.RootPath}   Files found: scanning...";
        lblCurrent.Text = "Current step: preparing run...";
        ResetResultsSummary();

        var progress = new Progress<MediaConverterProgress>(HandleProgress);
        var engine = new MediaConverterEngine();
        _runCancellation = new CancellationTokenSource();
        UpdateRunningState(true);

        try
        {
            var summary = await engine.RunAsync(options, progress, _runCancellation.Token);
            lblCurrent.Text = $"Current step: completed. Success: {summary.SuccessCount}, Skipped: {summary.SkipCount}, Failed: {summary.FailureCount}, Runtime: {MediaConverterEngine.FormatDuration(summary.Runtime)}";
        }
        catch (OperationCanceledException)
        {
            AddLogLine("Canceled.", ErrorLineColor);
            lblCurrent.Text = "Current step: canceled";
        }
        catch (Exception ex)
        {
            AddLogLine($"Fatal error: {ex.Message}", ErrorLineColor);
            MessageBox.Show(this, ex.Message, "Run Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblCurrent.Text = "Current step: failed";
        }
        finally
        {
            _runCancellation.Dispose();
            _runCancellation = null;
            UpdateRunningState(false);
        }
    }

    private void btnCancel_Click(object? sender, EventArgs e)
    {
        _runCancellation?.Cancel();
    }

    private void btnBrowseInput_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the input folder to process",
            InitialDirectory = Directory.Exists(txtInputFolder.Text) ? txtInputFolder.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtInputFolder.Text = dialog.SelectedPath;
        }
    }

    private void btnBrowseOutput_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the optional output folder",
            InitialDirectory = Directory.Exists(txtOutputFolder.Text) ? txtOutputFolder.Text : txtInputFolder.Text,
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtOutputFolder.Text = dialog.SelectedPath;
        }
    }

    private void btnBrowseMkvMerge_Click(object? sender, EventArgs e)
    {
        BrowseForExecutable(txtMkvMerge, "Select mkvmerge.exe", "mkvmerge.exe");
    }

    private void btnBrowseMkvPropEdit_Click(object? sender, EventArgs e)
    {
        BrowseForExecutable(txtMkvPropEdit, "Select mkvpropedit.exe", "mkvpropedit.exe");
    }

    private void btnToggleAdvanced_Click(object? sender, EventArgs e)
    {
        SetAdvancedVisibility(!panelAdvanced.Visible);
    }

    private void btnCopyLog_Click(object? sender, EventArgs e)
    {
        CopyFullLogToClipboard();
    }

    private void btnOpenOutputFolder_Click(object? sender, EventArgs e)
    {
        var outputFolder = txtOutputFolder.Text.Trim();
        var folderToOpen = string.IsNullOrWhiteSpace(outputFolder)
            ? txtInputFolder.Text.Trim()
            : outputFolder;

        if (string.IsNullOrWhiteSpace(folderToOpen) || !Directory.Exists(folderToOpen))
        {
            MessageBox.Show(this, $"Folder does not exist: {folderToOpen}", "Open Output Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folderToOpen,
            UseShellExecute = true,
        });
    }

    private void chkTraverseSubfolders_CheckedChanged(object? sender, EventArgs e)
    {
        UpdateSkipDirectoryState();
    }

    private void lstLog_DoubleClick(object? sender, EventArgs e)
    {
        if (lstLog.SelectedItem is LogEntry selectedEntry && !string.IsNullOrWhiteSpace(selectedEntry.Text))
        {
            Clipboard.SetText(selectedEntry.Text);
            lblCurrent.Text = "Copied selected log line to clipboard.";
        }
    }

    private MediaConverterOptions BuildOptions()
    {
        var inputFolder = txtInputFolder.Text.Trim();
        if (string.IsNullOrWhiteSpace(inputFolder))
        {
            throw new ArgumentException("Choose an input folder.");
        }

        if (!Directory.Exists(inputFolder))
        {
            throw new ArgumentException($"Input folder does not exist: {inputFolder}");
        }

        var mkvMergePath = txtMkvMerge.Text.Trim();
        if (string.IsNullOrWhiteSpace(mkvMergePath))
        {
            throw new ArgumentException("Enter the path to mkvmerge.exe.");
        }

        var mkvPropEditPath = txtMkvPropEdit.Text.Trim();
        if (string.IsNullOrWhiteSpace(mkvPropEditPath))
        {
            throw new ArgumentException("Enter the path to mkvpropedit.exe.");
        }

        var outputFolder = txtOutputFolder.Text.Trim();
        return new MediaConverterOptions
        {
            RootPath = Path.GetFullPath(inputFolder),
            OutputPath = string.IsNullOrWhiteSpace(outputFolder) ? null : Path.GetFullPath(outputFolder),
            OverwriteExisting = chkOverwrite.Checked,
            MkvMergePath = Path.GetFullPath(mkvMergePath),
            MkvPropEditPath = Path.GetFullPath(mkvPropEditPath),
            SkipFragment = string.IsNullOrWhiteSpace(txtSkip.Text) ? @"\Processing\" : txtSkip.Text.Trim(),
            TraverseSubfolders = chkTraverseSubfolders.Checked,
            MaintainFolderStructure = chkMaintainFolderStructure.Checked,
            DryRun = chkDryRun.Checked,
        };
    }

    private void HandleProgress(MediaConverterProgress update)
    {
        if (update.TotalFiles > 0)
        {
            progressOverall.Maximum = update.TotalFiles;
            var progressValue = (int)Math.Floor(update.PercentComplete * progressOverall.Maximum / 100m);
            progressOverall.Value = Math.Max(0, Math.Min(progressValue, progressOverall.Maximum));
        }
        else
        {
            progressOverall.Value = 0;
        }

        lblProgressPercent.Text = $"{update.PercentComplete:0.#}%";

        switch (update.Kind)
        {
            case MediaConverterEventKind.RunStarted:
                lblSummary.Text = $"Directory: {txtInputFolder.Text.Trim()}   Files found: {update.TotalFiles}";
                lblCurrent.Text = "Current step: starting run";
                lblResultsTotalValue.Text = update.TotalFiles.ToString();
                lblResultsRuntimeValue.Text = MediaConverterEngine.FormatDuration(update.Elapsed);
                AddLogLine(update.Message);
                break;

            case MediaConverterEventKind.FileStarted:
                lblCurrent.Text = $"Current step: starting {Path.GetFileName(update.FilePath)}";
                lblResultsRuntimeValue.Text = MediaConverterEngine.FormatDuration(update.Elapsed);
                AddHighlightedLogLine(update.Message);
                break;

            case MediaConverterEventKind.FileSkipped:
                _skipCount++;
                lblCurrent.Text = $"Current step: skipping {Path.GetFileName(update.FilePath)}";
                UpdateResultsSummary(update.Elapsed);
                AddHighlightedLogLine(update.Message);
                break;

            case MediaConverterEventKind.StageChanged:
                lblCurrent.Text = $"Current step: {update.Stage} ({Path.GetFileName(update.FilePath)})";
                lblResultsRuntimeValue.Text = MediaConverterEngine.FormatDuration(update.Elapsed);
                AddLogLine(update.Message);
                break;

            case MediaConverterEventKind.FileCompleted:
                _successCount++;
                lblCurrent.Text = $"Current step: completed {Path.GetFileName(update.FilePath)}";
                UpdateResultsSummary(update.Elapsed);
                AddMultiLineMessage(update.Message, OutputLineColor);
                break;

            case MediaConverterEventKind.FileFailed:
                _failureCount++;
                lblCurrent.Text = $"Current step: failed {Path.GetFileName(update.FilePath)}";
                UpdateResultsSummary(update.Elapsed);
                AddMultiLineMessage(update.Message, ErrorLineColor);
                break;

            case MediaConverterEventKind.RunCompleted:
                lblCurrent.Text = update.Summary is null
                    ? "Current step: completed"
                    : $"Current step: completed. Success: {update.Summary.SuccessCount}, Skipped: {update.Summary.SkipCount}, Failed: {update.Summary.FailureCount}, Runtime: {MediaConverterEngine.FormatDuration(update.Summary.Runtime)}";
                if (update.Summary is not null)
                {
                    _successCount = update.Summary.SuccessCount;
                    _skipCount = update.Summary.SkipCount;
                    _failureCount = update.Summary.FailureCount;
                    UpdateResultsSummary(update.Summary.Runtime);
                }
                AddMultiLineMessage(update.Message, SummaryLineColor);
                if (progressOverall.Maximum > 0)
                {
                    progressOverall.Value = progressOverall.Maximum;
                }

                lblProgressPercent.Text = "100%";
                break;
        }
    }

    private void AddMultiLineMessage(string message, Color? backColor = null)
    {
        foreach (var line in message.Split(Environment.NewLine))
        {
            if (backColor.HasValue)
            {
                AddLogLine(line, backColor.Value);
            }
            else
            {
                AddLogLine(line);
            }
        }
    }

    private void AddLogLine(string message)
    {
        AddLogEntry(new LogEntry(message, GetImplicitColor(message)));
    }

    private void AddLogLine(string message, Color backColor)
    {
        AddLogEntry(new LogEntry(message, backColor));
    }

    private void AddHighlightedLogLine(string message)
    {
        AddLogEntry(new LogEntry(message, StartLineColor));
    }

    private Color? GetImplicitColor(string message)
    {
        return message.Contains("[error]", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("error:", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("warning", StringComparison.OrdinalIgnoreCase)
            ? ErrorLineColor
            : null;
    }

    private void AddLogEntry(LogEntry entry)
    {
        lstLog.Items.Add(entry);
        lstLog.TopIndex = lstLog.Items.Count - 1;
        if (_runCancellation is null)
        {
            btnCopyLog.Enabled = lstLog.Items.Count > 0;
        }
    }

    private void CopyFullLogToClipboard()
    {
        if (lstLog.Items.Count == 0)
        {
            lblCurrent.Text = "There is no output to copy yet.";
            return;
        }

        var lines = lstLog.Items.Cast<LogEntry>()
            .Select(item => item.Text);
        Clipboard.SetText(string.Join(Environment.NewLine, lines));
        lblCurrent.Text = "Copied full log to clipboard.";
    }

    private void lstLog_DrawItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();

        if (e.Index < 0 || e.Index >= lstLog.Items.Count)
        {
            return;
        }

        var entry = (LogEntry)lstLog.Items[e.Index];
        var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var backgroundColor = isSelected ? SystemColors.Highlight : entry.BackColor ?? lstLog.BackColor;
        var textColor = isSelected ? SystemColors.HighlightText : lstLog.ForeColor;

        using var backgroundBrush = new SolidBrush(backgroundColor);
        using var textBrush = new SolidBrush(textColor);

        e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
        var textBounds = Rectangle.Inflate(e.Bounds, -4, -2);
        e.Graphics.DrawString(entry.Text, e.Font ?? lstLog.Font, textBrush, textBounds);
        e.DrawFocusRectangle();
    }

    private void UpdateLogItemHeight()
    {
        var textHeight = TextRenderer.MeasureText("Ag", lstLog.Font).Height;
        lstLog.ItemHeight = Math.Max(textHeight + 4, 20);
    }

    private void ResetResultsSummary()
    {
        _successCount = 0;
        _skipCount = 0;
        _failureCount = 0;
        lblResultsTotalValue.Text = "0";
        lblResultsSuccessValue.Text = "0";
        lblResultsSkippedValue.Text = "0";
        lblResultsFailedValue.Text = "0";
        lblResultsRuntimeValue.Text = "0:00";
    }

    private void UpdateResultsSummary(TimeSpan runtime)
    {
        lblResultsSuccessValue.Text = _successCount.ToString();
        lblResultsSkippedValue.Text = _skipCount.ToString();
        lblResultsFailedValue.Text = _failureCount.ToString();
        lblResultsRuntimeValue.Text = MediaConverterEngine.FormatDuration(runtime);
    }

    private void ApplyResultsSummaryColors()
    {
        lblResultsTotalValue.ForeColor = Color.FromArgb(35, 35, 35);
        lblResultsSuccessValue.ForeColor = Color.FromArgb(38, 110, 38);
        lblResultsSkippedValue.ForeColor = Color.FromArgb(120, 90, 20);
        lblResultsFailedValue.ForeColor = Color.FromArgb(170, 40, 40);
        lblResultsRuntimeValue.ForeColor = Color.FromArgb(0, 90, 140);
    }

    private void UpdateSkipDirectoryState()
    {
        var skipEnabled = chkTraverseSubfolders.Checked && _runCancellation is null;
        lblSkip.Enabled = skipEnabled;
        txtSkip.Enabled = skipEnabled;
        chkMaintainFolderStructure.Enabled = skipEnabled;
    }

    private void BrowseForExecutable(TextBox targetTextBox, string title, string defaultFileName)
    {
        using var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
            FileName = defaultFileName,
            InitialDirectory = GetInitialDirectory(targetTextBox.Text),
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            targetTextBox.Text = dialog.FileName;
        }
    }

    private static string GetInitialDirectory(string path)
    {
        if (File.Exists(path))
        {
            return Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
        }

        if (Directory.Exists(path))
        {
            return path;
        }

        return Environment.CurrentDirectory;
    }

    private void SetAdvancedVisibility(bool visible)
    {
        panelAdvanced.Visible = visible;
        btnToggleAdvanced.Text = visible ? "Hide Settings" : "Show Settings";
    }

    private void UpdateRunningState(bool isRunning)
    {
        btnStart.Enabled = !isRunning;
        btnCancel.Enabled = isRunning;
        btnCopyLog.Enabled = !isRunning && lstLog.Items.Count > 0;
        btnOpenOutputFolder.Enabled = !isRunning;
        btnToggleAdvanced.Enabled = !isRunning;
        btnBrowseInput.Enabled = !isRunning;
        btnBrowseOutput.Enabled = !isRunning;
        btnBrowseMkvMerge.Enabled = !isRunning;
        btnBrowseMkvPropEdit.Enabled = !isRunning;
        txtInputFolder.Enabled = !isRunning;
        txtOutputFolder.Enabled = !isRunning;
        chkOverwrite.Enabled = !isRunning;
        txtSkip.Enabled = !isRunning;
        txtMkvMerge.Enabled = !isRunning;
        txtMkvPropEdit.Enabled = !isRunning;
        chkDryRun.Enabled = !isRunning;
        chkTraverseSubfolders.Enabled = !isRunning;
        UpdateSkipDirectoryState();
        if (!isRunning && progressOverall.Value == 0)
        {
            lblProgressPercent.Text = "0%";
        }
    }
}

internal sealed record LogEntry(string Text, Color? BackColor = null)
{
    public override string ToString() => Text;
}
