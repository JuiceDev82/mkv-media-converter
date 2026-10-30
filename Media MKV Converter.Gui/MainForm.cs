using Media_MKV_Converter.Core;
using System.Diagnostics;

namespace Media_MKV_Converter.Gui;

public partial class MainForm : Form
{
    private static readonly Color StartLineColor = Color.FromArgb(220, 235, 255);
    private static readonly Color OutputLineColor = Color.FromArgb(225, 245, 225);
    private static readonly Color SummaryLineColor = Color.FromArgb(183, 225, 183);
    private static readonly Color ErrorLineColor = Color.FromArgb(255, 220, 220);

    /// <summary>The one preset name that auto-loads on every startup instead of leaving the picker
    /// blank, so saving over it is the normal "make this the new default" workflow — every other
    /// preset still has to be picked by hand.</summary>
    private const string DefaultPresetName = "Default";

    private CancellationTokenSource? _runCancellation;
    private int _successCount;
    private int _skipCount;
    private int _failureCount;

    private readonly System.Windows.Forms.Timer _filePreviewDebounceTimer;
    private readonly FileSystemWatcher _filePreviewWatcher;
    private int _filePreviewRequestVersion;
    private bool _suppressFilePreviewItemCheck;
    private bool _suppressPresetSelectionChanged;

    public MainForm()
    {
        InitializeComponent();
        ApplyApplicationIcon();

        // Created first: setting the fields below fires TextChanged handlers that touch this timer.
        _filePreviewDebounceTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _filePreviewDebounceTimer.Tick += FilePreviewDebounceTimer_Tick;
        FormClosed += (_, _) => _filePreviewDebounceTimer.Dispose();

        // Not watching anything until the first successful scan points it at a real folder.
        // SynchronizingObject makes WinForms marshal every event below onto the UI thread for us,
        // so the handlers can touch _filePreviewDebounceTimer directly.
        _filePreviewWatcher = new FileSystemWatcher
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            SynchronizingObject = this,
        };
        _filePreviewWatcher.Created += (_, _) => RestartFilePreviewDebounce();
        _filePreviewWatcher.Deleted += (_, _) => RestartFilePreviewDebounce();
        _filePreviewWatcher.Renamed += (_, _) => RestartFilePreviewDebounce();
        _filePreviewWatcher.Error += (_, _) => RestartFilePreviewDebounce();
        FormClosed += (_, _) => _filePreviewWatcher.Dispose();

        txtInputFolder.Text = Environment.CurrentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var savedToolPaths = ToolPathSettingsStore.Load();
        txtMkvToolNixFolder.Text = savedToolPaths.MkvToolNixFolder ?? string.Empty;
        txtMkvToolNixFolder.Leave += (_, _) => SaveToolPaths();

        txtSkip.Text = @"\Processing\";
        txtFileFormats.Text = DefaultFileFormatsText();
        chkOverwrite.Checked = true;
        chkTraverseSubfolders.Checked = true;
        chkMaintainFolderStructure.Checked = false;
        chkExcludeCommentary.Checked = true;
        PopulateLanguages();
        PopulatePresetList();
        SelectDefaultPresetOnStartup();
        SetAdvancedVisibility(false);
        UpdateRunningState(false);
        UpdateSkipDirectoryState();
        ApplyResultsSummaryColors();
        UpdateLogItemHeight();

        // Show the initial folder's files right away rather than waiting out the debounce.
        _filePreviewDebounceTimer.Stop();
        _ = RefreshFilePreviewAsync();
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

            // A run can rename or delete files in the input folder, so the preview needs a re-scan.
            _ = RefreshFilePreviewAsync();
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

    private void btnBrowseMkvToolNixFolder_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder where MKVToolNix is installed",
            InitialDirectory = Directory.Exists(txtMkvToolNixFolder.Text) ? txtMkvToolNixFolder.Text : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtMkvToolNixFolder.Text = dialog.SelectedPath;
            SaveToolPaths();
        }
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
        RestartFilePreviewDebounce();
    }

    private void txtInputFolder_TextChanged(object? sender, EventArgs e)
    {
        RestartFilePreviewDebounce();
    }

    private void txtSkip_TextChanged(object? sender, EventArgs e)
    {
        RestartFilePreviewDebounce();
    }

    private void txtFileFormats_TextChanged(object? sender, EventArgs e)
    {
        RestartFilePreviewDebounce();
    }

    private void FilePreviewDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _filePreviewDebounceTimer.Stop();
        _ = RefreshFilePreviewAsync();
    }

    private void lstLog_DoubleClick(object? sender, EventArgs e)
    {
        if (lstLog.SelectedItem is LogEntry selectedEntry && !string.IsNullOrWhiteSpace(selectedEntry.Text))
        {
            Clipboard.SetText(selectedEntry.Text);
            lblCurrent.Text = "Copied selected log line to clipboard.";
        }
    }

    /// <summary>
    /// Sets the running window's icon (title bar, taskbar button, minimized, Alt-Tab) from the same
    /// icon <c>&lt;ApplicationIcon&gt;</c> already embedded into this exe at build time — one source
    /// of truth rather than a second loose copy that could drift out of sync. Without this, the
    /// exe's own icon shows correctly everywhere Explorer looks, but the window falls back to
    /// WinForms' generic default icon while it's actually running, which is what minimizing shows.
    /// </summary>
    private void ApplyApplicationIcon()
    {
        try
        {
            using var extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (extracted is not null)
            {
                Icon = (Icon)extracted.Clone();
            }
        }
        catch (Exception)
        {
            // Best-effort and purely cosmetic: no icon is worse than a missing one, never worth
            // failing the app over, regardless of what GDI+ throws.
        }
    }

    private void RestartFilePreviewDebounce()
    {
        _filePreviewDebounceTimer.Stop();
        _filePreviewDebounceTimer.Start();
    }

    private void btnRefreshFilePreview_Click(object? sender, EventArgs e)
    {
        _filePreviewDebounceTimer.Stop();
        _ = RefreshFilePreviewAsync();
    }

    /// <summary>
    /// Points <see cref="_filePreviewWatcher"/> at the folder a scan just resolved, or turns it off
    /// when there is nothing valid to watch. Watching is a convenience on top of the Refresh button
    /// and the explicit refresh triggers, not a guarantee — some network or virtual drives refuse
    /// it, silently swallowed here rather than left as an unhandled exception on a background event.
    /// </summary>
    private void UpdateFilePreviewWatcher(string? validatedRootPath)
    {
        if (validatedRootPath is null)
        {
            _filePreviewWatcher.EnableRaisingEvents = false;
            return;
        }

        try
        {
            if (!string.Equals(_filePreviewWatcher.Path, validatedRootPath, StringComparison.OrdinalIgnoreCase))
            {
                _filePreviewWatcher.EnableRaisingEvents = false;
                _filePreviewWatcher.Path = validatedRootPath;
            }

            _filePreviewWatcher.IncludeSubdirectories = chkTraverseSubfolders.Checked;
            _filePreviewWatcher.EnableRaisingEvents = true;
        }
        catch (Exception)
        {
            _filePreviewWatcher.EnableRaisingEvents = false;
        }
    }

    /// <summary>
    /// Re-scans the input folder and repopulates the preview list. Runs the scan on a background
    /// thread so a large or slow (e.g. network) folder does not freeze the UI, and guards against
    /// out-of-order results: if the folder or a setting changes again before this scan finishes, a
    /// newer call's higher <see cref="_filePreviewRequestVersion"/> makes this one's result a no-op.
    /// Manual exclusions (unchecked boxes) are preserved by full path across the refresh, so a live
    /// update from the file watcher — an unrelated file appearing elsewhere in the folder — does not
    /// silently discard a selection the user already made.
    /// </summary>
    private async Task RefreshFilePreviewAsync()
    {
        var requestVersion = ++_filePreviewRequestVersion;
        var inputFolder = txtInputFolder.Text.Trim();
        var previouslyExcludedPaths = GetManuallyExcludedPaths();

        SetFilePreviewItems([]);

        if (string.IsNullOrWhiteSpace(inputFolder))
        {
            lblFilePreviewSummary.Text = "Choose an input folder to preview its files.";
            UpdateFilePreviewWatcher(null);
            return;
        }

        string rootPath;
        try
        {
            rootPath = Path.GetFullPath(inputFolder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            lblFilePreviewSummary.Text = $"Invalid folder path: {ex.Message}";
            UpdateFilePreviewWatcher(null);
            return;
        }

        if (!Directory.Exists(rootPath))
        {
            lblFilePreviewSummary.Text = "Folder does not exist.";
            UpdateFilePreviewWatcher(null);
            return;
        }

        UpdateFilePreviewWatcher(rootPath);

        var previewOptions = new MediaConverterOptions
        {
            RootPath = rootPath,
            TraverseSubfolders = chkTraverseSubfolders.Checked,
            SkipFragment = string.IsNullOrWhiteSpace(txtSkip.Text) ? @"\Processing\" : txtSkip.Text.Trim(),
            FileFormats = FileFormatParser.Parse(txtFileFormats.Text),
        };

        lblFilePreviewSummary.Text = "Scanning...";

        IReadOnlyList<FilePreviewEntry> entries;
        try
        {
            entries = await Task.Run(() => new MediaConverterEngine().PreviewFiles(previewOptions));
        }
        catch (Exception ex)
        {
            if (requestVersion == _filePreviewRequestVersion)
            {
                lblFilePreviewSummary.Text = $"Could not list files: {ex.Message}";
            }

            return;
        }

        if (requestVersion != _filePreviewRequestVersion)
        {
            // Superseded by a newer scan (folder or a setting changed again); let that one win.
            return;
        }

        var items = entries.Select(entry =>
        {
            var relativePath = Path.GetRelativePath(rootPath, entry.FullPath);
            var displayText = BuildFilePreviewDisplayText(relativePath, entry.WillBeSkipped);
            var isChecked = !entry.WillBeSkipped && !previouslyExcludedPaths.Contains(entry.FullPath);
            return (Item: new FilePreviewListItem(displayText, relativePath, entry.FullPath, entry.WillBeSkipped), Checked: isChecked);
        }).ToList();

        SetFilePreviewItems(items);

        if (entries.Count == 0)
        {
            lblFilePreviewSummary.Text = $"No supported files found under {rootPath}.";
        }
        else
        {
            UpdateFilePreviewSummary();
        }
    }

    /// <summary>"relativePath" normally, "relativePath  (skip)" for a skip-fragment match, or
    /// "relativePath  (newName.mkv)" when Trim Name After / Remove From Name would actually rename
    /// it — the three are mutually exclusive, there is no point previewing a rename for a file that
    /// will not be processed anyway.</summary>
    private string BuildFilePreviewDisplayText(string relativePath, bool willBeSkipped)
    {
        if (willBeSkipped)
        {
            return $"{relativePath}  (skip)";
        }

        var renamedTo = ComputeFilePreviewRenameHint(relativePath);
        return renamedTo is null ? relativePath : $"{relativePath}  ({renamedTo})";
    }

    /// <summary>
    /// The bare file name Trim Name After / Remove From Name / Replace In Name would currently
    /// produce for this file, or null when there is nothing to show: all three fields are blank, the
    /// current Trim Name After text is not a pattern that could actually run (the same check
    /// BuildOptions() uses to block a run — showing a hint for a pattern that would be rejected at
    /// Start would be misleading), or none of the three settings ends up changing the name for this
    /// particular file. Comparison ignores the extension, since PreviewOutputFileName always appends
    /// ".mkv" regardless of source format and that swap happens on every run independent of these
    /// three settings — not what this hint is about.
    /// </summary>
    private string? ComputeFilePreviewRenameHint(string relativePath)
    {
        var trimNameAfter = txtTrimName.Text.Trim();
        var removeFromName = FileNameStringRemover.ParsePatterns(txtRemoveFromName.Text);
        var replaceInName = FileNameReplacer.ParsePairs(txtReplaceInName.Text);

        if (trimNameAfter.Length == 0 && removeFromName.Count == 0 && replaceInName.Count == 0)
        {
            return null;
        }

        if (!FileNameTrimmer.IsValidPattern(trimNameAfter))
        {
            return null;
        }

        var originalFileName = Path.GetFileName(relativePath);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
        var newFileName = MediaConverterEngine.PreviewOutputFileName(nameWithoutExtension, trimNameAfter, removeFromName, replaceInName);
        var newNameWithoutExtension = Path.GetFileNameWithoutExtension(newFileName);

        return string.Equals(newNameWithoutExtension, nameWithoutExtension, StringComparison.Ordinal) ? null : newFileName;
    }

    /// <summary>
    /// Recomputes every non-skip row's rename hint in place from the current Trim Name After /
    /// Remove From Name text, without re-scanning the folder — unlike RefreshFilePreviewAsync, this
    /// is a pure, synchronous string computation with no file-system access, so it runs directly off
    /// every keystroke with no debounce needed. Only rows whose hint text actually changed are
    /// replaced; replacing CheckedListBox.Items[i] does not disturb that row's check state, since
    /// checked-ness is tracked per index, not per object, so a manual exclusion survives untouched.
    /// </summary>
    private void UpdateFilePreviewNameHints()
    {
        _suppressFilePreviewItemCheck = true;
        try
        {
            for (var i = 0; i < lstFilePreview.Items.Count; i++)
            {
                if (lstFilePreview.Items[i] is not FilePreviewListItem item)
                {
                    continue;
                }

                var displayText = BuildFilePreviewDisplayText(item.RelativePath, item.WillBeSkipped);
                if (!string.Equals(displayText, item.DisplayText, StringComparison.Ordinal))
                {
                    lstFilePreview.Items[i] = item with { DisplayText = displayText };
                }
            }
        }
        finally
        {
            _suppressFilePreviewItemCheck = false;
        }
    }

    private void txtTrimName_TextChanged(object? sender, EventArgs e)
    {
        UpdateFilePreviewNameHints();
    }

    private void txtRemoveFromName_TextChanged(object? sender, EventArgs e)
    {
        UpdateFilePreviewNameHints();
    }

    private void txtReplaceInName_TextChanged(object? sender, EventArgs e)
    {
        UpdateFilePreviewNameHints();
    }

    /// <summary>Full paths of files unchecked by the user — never a skip-fragment match, since those
    /// are already excluded and their checkbox is locked. Doubles as the set fed into a real run's
    /// <see cref="MediaConverterOptions.ExcludedPaths"/> and as the memory preserved across a refresh.</summary>
    private HashSet<string> GetManuallyExcludedPaths()
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < lstFilePreview.Items.Count; i++)
        {
            if (lstFilePreview.Items[i] is FilePreviewListItem { WillBeSkipped: false } item && !lstFilePreview.GetItemChecked(i))
            {
                excluded.Add(item.FullPath);
            }
        }

        return excluded;
    }

    /// <summary>Replaces the preview list's contents in one shot, with per-item check state, without
    /// the population itself triggering <see cref="lstFilePreview_ItemCheck"/>'s side effects.</summary>
    private void SetFilePreviewItems(IReadOnlyList<(FilePreviewListItem Item, bool Checked)> items)
    {
        _suppressFilePreviewItemCheck = true;
        try
        {
            lstFilePreview.BeginUpdate();
            lstFilePreview.Items.Clear();
            foreach (var (item, isChecked) in items)
            {
                var index = lstFilePreview.Items.Add(item);
                lstFilePreview.SetItemChecked(index, isChecked);
            }

            lstFilePreview.EndUpdate();
        }
        finally
        {
            _suppressFilePreviewItemCheck = false;
        }
    }

    /// <summary>
    /// Skip-fragment matches are excluded unconditionally — that rule is a deliberate, path-based
    /// safety net (e.g. protecting an active-download folder), and letting a checkbox silently
    /// override it would undermine the point of setting it. CheckedListBox has no per-item disabled
    /// state, so the lock is implemented by reverting any attempted toggle on those specific rows.
    /// </summary>
    private void lstFilePreview_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_suppressFilePreviewItemCheck)
        {
            return;
        }

        if (e.Index >= 0 && e.Index < lstFilePreview.Items.Count &&
            lstFilePreview.Items[e.Index] is FilePreviewListItem { WillBeSkipped: true })
        {
            e.NewValue = CheckState.Unchecked;
            return;
        }

        // ItemCheck fires before the new state commits; defer so the recount sees it.
        BeginInvoke(UpdateFilePreviewSummary);
    }

    private void UpdateFilePreviewSummary()
    {
        var total = lstFilePreview.Items.Count;
        if (total == 0)
        {
            return;
        }

        var skipFragmentCount = 0;
        var manuallyExcludedCount = 0;
        for (var i = 0; i < total; i++)
        {
            if (lstFilePreview.Items[i] is not FilePreviewListItem item)
            {
                continue;
            }

            if (item.WillBeSkipped)
            {
                skipFragmentCount++;
            }
            else if (!lstFilePreview.GetItemChecked(i))
            {
                manuallyExcludedCount++;
            }
        }

        var parts = new List<string> { $"{total} file(s) found" };
        if (skipFragmentCount > 0)
        {
            parts.Add($"{skipFragmentCount} will be skipped");
        }

        if (manuallyExcludedCount > 0)
        {
            parts.Add($"{manuallyExcludedCount} manually excluded");
        }

        lblFilePreviewSummary.Text = string.Join(", ", parts) + ".";
    }

    private void PopulateLanguages()
    {
        clbLanguages.DisplayMember = nameof(LanguageOption.DisplayName);

        foreach (var option in LanguageCatalog.All)
        {
            var index = clbLanguages.Items.Add(option);
            clbLanguages.SetItemChecked(index, option.Code == LanguageCatalog.DefaultCode);
        }
    }

    /// <summary>(Re)loads the preset list from disk into the combo box, preserving the current
    /// selection by name when it still exists (e.g. after a save that didn't rename anything).</summary>
    private void PopulatePresetList()
    {
        _suppressPresetSelectionChanged = true;
        try
        {
            var selectedName = cboPreset.SelectedItem is ConversionPreset selected ? selected.Name : null;

            cboPreset.Items.Clear();
            foreach (var preset in PresetStore.Load().OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                cboPreset.Items.Add(preset);
            }

            cboPreset.SelectedItem = selectedName is null
                ? null
                : cboPreset.Items.Cast<ConversionPreset>().FirstOrDefault(p => p.Name == selectedName);
        }
        finally
        {
            _suppressPresetSelectionChanged = false;
        }

        btnDeletePreset.Enabled = cboPreset.SelectedItem is not null;
    }

    /// <summary>Called once at startup, after <see cref="PopulatePresetList"/>. If a preset named
    /// "Default" has been saved, selects it — which fires <see cref="cboPreset_SelectedIndexChanged"/>
    /// and applies it, same as picking it by hand. If one hasn't been saved yet, this is a no-op and
    /// the picker starts blank as before, since there's nothing to load.</summary>
    private void SelectDefaultPresetOnStartup()
    {
        var defaultPreset = cboPreset.Items.Cast<ConversionPreset>()
            .FirstOrDefault(p => string.Equals(p.Name, DefaultPresetName, StringComparison.OrdinalIgnoreCase));

        // First-ever launch, or the saved "Default" preset was deleted: create one from whatever
        // the form's own hardcoded startup values are (set earlier in the constructor), so there's
        // always a real "Default" to show and build on, rather than requiring a manual Save first.
        if (defaultPreset is null)
        {
            defaultPreset = BuildPresetFromCurrentFields(DefaultPresetName);
            if (defaultPreset is not null)
            {
                var presets = PresetStore.Load();
                presets.Add(defaultPreset);
                try
                {
                    PresetStore.Save(presets);
                }
                catch (Exception)
                {
                    // Best-effort, same as ToolPathSettingsStore: a transient I/O error here just
                    // means the picker re-attempts this on the next launch instead of surfacing
                    // a dialog for something the user didn't explicitly ask to happen.
                    return;
                }

                PopulatePresetList();
                defaultPreset = cboPreset.Items.Cast<ConversionPreset>()
                    .FirstOrDefault(p => string.Equals(p.Name, DefaultPresetName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (defaultPreset is not null)
        {
            cboPreset.SelectedItem = defaultPreset;
        }
    }

    /// <summary>The File Formats field's own default value, as comma-separated text with no leading
    /// dots — reads <see cref="MediaConverterOptions.FileFormats"/>'s own default rather than
    /// repeating the literal "mkv,mp4,avi" a second time, so the two can't drift apart.</summary>
    private static string DefaultFileFormatsText() =>
        string.Join(',', new MediaConverterOptions().FileFormats.Select(extension => extension.TrimStart('.')));

    /// <summary>Builds a <see cref="ConversionPreset"/> from the current state of every field a
    /// preset covers. Shared by the explicit Save button and the first-launch auto-create of
    /// "Default", so the two can never map fields differently. Returns null when no language is
    /// checked — a preset with no languages would fail <c>BuildOptions</c>' own validation anyway.</summary>
    private ConversionPreset? BuildPresetFromCurrentFields(string name)
    {
        var languages = clbLanguages.CheckedItems.Cast<LanguageOption>().Select(option => option.Code).ToList();
        if (languages.Count == 0)
        {
            return null;
        }

        return new ConversionPreset
        {
            Name = name,
            Languages = languages,
            PrimaryLanguage = string.IsNullOrWhiteSpace(txtPrimaryLanguage.Text) ? null : txtPrimaryLanguage.Text.Trim(),
            ExcludeCommentaryTracks = chkExcludeCommentary.Checked,
            // Not collapsed to null when blank, unlike every other optional field here: null means
            // "this preset predates the File Formats field," which ApplyPreset falls back to the
            // default list for, while "" means the user deliberately cleared it to process every
            // file and saved that choice — the two need to stay distinguishable.
            FileFormats = txtFileFormats.Text.Trim(),
            TrimFromStart = string.IsNullOrWhiteSpace(txtTrimStart.Text) ? null : txtTrimStart.Text.Trim(),
            TrimFromEnd = string.IsNullOrWhiteSpace(txtTrimEnd.Text) ? null : txtTrimEnd.Text.Trim(),
            TrimNameAfter = string.IsNullOrWhiteSpace(txtTrimName.Text) ? null : txtTrimName.Text.Trim(),
            RemoveFromName = string.IsNullOrWhiteSpace(txtRemoveFromName.Text) ? null : txtRemoveFromName.Text.Trim(),
            // Not trimmed: a trailing space can be the replacement value itself (the headline use
            // case, "replace periods with spaces"), and trimming the whole field would silently eat it.
            ReplaceInName = string.IsNullOrEmpty(txtReplaceInName.Text) ? null : txtReplaceInName.Text,
        };
    }

    private void cboPreset_SelectedIndexChanged(object? sender, EventArgs e)
    {
        btnDeletePreset.Enabled = cboPreset.SelectedItem is not null;

        if (_suppressPresetSelectionChanged || cboPreset.SelectedItem is not ConversionPreset preset)
        {
            return;
        }

        ApplyPreset(preset);
    }

    private void ApplyPreset(ConversionPreset preset)
    {
        for (var i = 0; i < clbLanguages.Items.Count; i++)
        {
            var option = (LanguageOption)clbLanguages.Items[i]!;
            clbLanguages.SetItemChecked(i, preset.Languages.Contains(option.Code, StringComparer.OrdinalIgnoreCase));
        }

        txtPrimaryLanguage.Text = preset.PrimaryLanguage ?? string.Empty;
        chkExcludeCommentary.Checked = preset.ExcludeCommentaryTracks;
        txtFileFormats.Text = preset.FileFormats ?? DefaultFileFormatsText();
        txtTrimStart.Text = preset.TrimFromStart ?? string.Empty;
        txtTrimEnd.Text = preset.TrimFromEnd ?? string.Empty;
        txtTrimName.Text = preset.TrimNameAfter ?? string.Empty;
        txtRemoveFromName.Text = preset.RemoveFromName ?? string.Empty;
        txtReplaceInName.Text = preset.ReplaceInName ?? string.Empty;
    }

    private void btnSavePreset_Click(object? sender, EventArgs e)
    {
        var currentName = cboPreset.SelectedItem is ConversionPreset selected ? selected.Name : string.Empty;

        using var dialog = new PresetNameDialog("Save Preset", currentName);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var name = dialog.PresetName;
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Enter a name for the preset.", "Save Preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var presets = PresetStore.Load();
        var existing = presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            var overwrite = MessageBox.Show(
                this,
                $"A preset named \"{name}\" already exists. Overwrite it?",
                "Save Preset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (overwrite != DialogResult.Yes)
            {
                return;
            }

            presets.Remove(existing);
        }

        var preset = BuildPresetFromCurrentFields(name);
        if (preset is null)
        {
            MessageBox.Show(this, "Select at least one language before saving a preset.", "Save Preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        presets.Add(preset);

        try
        {
            PresetStore.Save(presets);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save the preset: {ex.Message}", "Save Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        PopulatePresetList();
    }

    private void btnDeletePreset_Click(object? sender, EventArgs e)
    {
        if (cboPreset.SelectedItem is not ConversionPreset selected)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Delete the preset \"{selected.Name}\"?",
            "Delete Preset",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        var presets = PresetStore.Load();
        presets.RemoveAll(p => string.Equals(p.Name, selected.Name, StringComparison.OrdinalIgnoreCase));

        try
        {
            PresetStore.Save(presets);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not delete the preset: {ex.Message}", "Delete Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        PopulatePresetList();
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

        var mkvToolNixFolder = txtMkvToolNixFolder.Text.Trim();
        if (string.IsNullOrWhiteSpace(mkvToolNixFolder))
        {
            throw new ArgumentException("Select the folder where MKVToolNix is installed.");
        }

        // mkvmerge.exe, mkvpropedit.exe, and mkvtoolnix-gui.exe all live in this one folder — actual
        // existence is checked once, centrally, by MediaConverterEngine.ValidateOptions.
        var mkvMergePath = Path.Combine(mkvToolNixFolder, "mkvmerge.exe");
        var mkvPropEditPath = Path.Combine(mkvToolNixFolder, "mkvpropedit.exe");
        var mkvToolNixGuiPath = Path.Combine(mkvToolNixFolder, "mkvtoolnix-gui.exe");

        if (!DurationParser.TryParse(txtTrimStart.Text, out var trimFromStart))
        {
            throw new ArgumentException(
                $"'{txtTrimStart.Text.Trim()}' is not a valid duration. Try 10m, 90s, 1h30m or 00:10:00.");
        }

        if (!DurationParser.TryParse(txtTrimEnd.Text, out var trimFromEnd))
        {
            throw new ArgumentException(
                $"'{txtTrimEnd.Text.Trim()}' is not a valid duration. Try 10m, 90s, 1h30m or 00:10:00.");
        }

        var trimNameAfter = txtTrimName.Text.Trim();
        if (!FileNameTrimmer.IsValidPattern(trimNameAfter))
        {
            throw new ArgumentException(
                "The name pattern needs at least one ordinary character; a pattern of only '*' would match every name at its start.");
        }

        var removeFromName = FileNameStringRemover.ParsePatterns(txtRemoveFromName.Text);
        var replaceInName = FileNameReplacer.ParsePairs(txtReplaceInName.Text);

        var languages = clbLanguages.CheckedItems
            .Cast<LanguageOption>()
            .Select(option => option.Code)
            .ToList();

        if (languages.Count == 0)
        {
            throw new ArgumentException("Select at least one language to keep.");
        }

        var primaryLanguage = txtPrimaryLanguage.Text.Trim();
        if (!string.IsNullOrWhiteSpace(primaryLanguage))
        {
            if (!LanguageCatalog.IsValidCode(primaryLanguage))
            {
                throw new ArgumentException(
                    $"'{primaryLanguage}' is not a valid primary language code. Use an ISO 639 code such as eng, spa or fre.");
            }

            if (!LanguageCatalog.IsSelected(LanguageCatalog.Normalize(languages), primaryLanguage))
            {
                throw new ArgumentException("The primary language must also be checked in the Languages list.");
            }
        }

        var outputFolder = txtOutputFolder.Text.Trim();
        return new MediaConverterOptions
        {
            Languages = languages,
            PrimaryLanguage = string.IsNullOrWhiteSpace(primaryLanguage) ? null : primaryLanguage,
            ExcludeCommentaryTracks = chkExcludeCommentary.Checked,
            ExcludedPaths = GetManuallyExcludedPaths(),
            MaxParallelFiles = (int)numParallelFiles.Value,
            TrimFromStart = trimFromStart,
            TrimFromEnd = trimFromEnd,
            TrimNameAfter = string.IsNullOrWhiteSpace(trimNameAfter) ? null : trimNameAfter,
            RemoveFromName = removeFromName,
            ReplaceInName = replaceInName,
            RootPath = Path.GetFullPath(inputFolder),
            OutputPath = string.IsNullOrWhiteSpace(outputFolder) ? null : Path.GetFullPath(outputFolder),
            OverwriteExisting = chkOverwrite.Checked,
            MkvMergePath = Path.GetFullPath(mkvMergePath),
            MkvPropEditPath = Path.GetFullPath(mkvPropEditPath),
            MkvToolNixGuiPath = Path.GetFullPath(mkvToolNixGuiPath),
            OpenInMkvToolNixGui = chkVerifyInMkvToolNix.Checked,
            SkipFragment = string.IsNullOrWhiteSpace(txtSkip.Text) ? @"\Processing\" : txtSkip.Text.Trim(),
            FileFormats = FileFormatParser.Parse(txtFileFormats.Text),
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

    private void SaveToolPaths()
    {
        ToolPathSettingsStore.Save(new ToolPathSettings
        {
            MkvToolNixFolder = string.IsNullOrWhiteSpace(txtMkvToolNixFolder.Text) ? null : txtMkvToolNixFolder.Text.Trim(),
        });
    }

    private void SetAdvancedVisibility(bool visible)
    {
        panelAdvanced.Visible = visible;
        btnToggleAdvanced.Text = visible ? "Hide Settings" : "Show Settings";
    }

    private void UpdateRunningState(bool isRunning)
    {
        // A run creates/deletes/renames files in the very folder being watched (temp files, output
        // moves); suspend the watcher for the duration so that churn doesn't fight the run for the
        // preview list, instead of just fighting for it more slowly.
        try
        {
            _filePreviewWatcher.EnableRaisingEvents = !isRunning && _filePreviewWatcher.Path.Length > 0;
        }
        catch (Exception)
        {
            // The watched folder could in principle vanish between calls; watching is best-effort.
        }

        btnRefreshFilePreview.Enabled = !isRunning;

        btnStart.Enabled = !isRunning;
        btnCancel.Enabled = isRunning;
        btnCopyLog.Enabled = !isRunning && lstLog.Items.Count > 0;
        btnOpenOutputFolder.Enabled = !isRunning;
        btnToggleAdvanced.Enabled = !isRunning;
        btnBrowseInput.Enabled = !isRunning;
        btnBrowseOutput.Enabled = !isRunning;
        btnBrowseMkvToolNixFolder.Enabled = !isRunning;
        txtInputFolder.Enabled = !isRunning;
        txtOutputFolder.Enabled = !isRunning;
        chkOverwrite.Enabled = !isRunning;
        txtSkip.Enabled = !isRunning;
        lblFileFormats.Enabled = !isRunning;
        txtFileFormats.Enabled = !isRunning;
        txtMkvToolNixFolder.Enabled = !isRunning;
        chkVerifyInMkvToolNix.Enabled = !isRunning;
        chkDryRun.Enabled = !isRunning;
        chkTraverseSubfolders.Enabled = !isRunning;
        clbLanguages.Enabled = !isRunning;
        lblLanguages.Enabled = !isRunning;
        txtTrimStart.Enabled = !isRunning;
        lblTrimStart.Enabled = !isRunning;
        txtTrimEnd.Enabled = !isRunning;
        lblTrimEnd.Enabled = !isRunning;
        txtPrimaryLanguage.Enabled = !isRunning;
        lblPrimaryLanguage.Enabled = !isRunning;
        chkExcludeCommentary.Enabled = !isRunning;
        numParallelFiles.Enabled = !isRunning;
        lstFilePreview.Enabled = !isRunning;
        cboPreset.Enabled = !isRunning;
        btnSavePreset.Enabled = !isRunning;
        btnDeletePreset.Enabled = !isRunning && cboPreset.SelectedItem is not null;
        txtTrimName.Enabled = !isRunning;
        lblTrimName.Enabled = !isRunning;
        txtRemoveFromName.Enabled = !isRunning;
        lblRemoveFromName.Enabled = !isRunning;
        txtReplaceInName.Enabled = !isRunning;
        lblReplaceInName.Enabled = !isRunning;
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

/// <summary>One row in the file preview <see cref="CheckedListBox"/>. <see cref="RelativePath"/> is
/// the clean path with no "(skip)"/rename-hint decoration, kept separately so
/// <see cref="MainForm.UpdateFilePreviewNameHints"/> can recompute <see cref="DisplayText"/> from it
/// without re-deriving it from <see cref="FullPath"/> and the current root folder each time.</summary>
internal sealed record FilePreviewListItem(string DisplayText, string RelativePath, string FullPath, bool WillBeSkipped)
{
    public override string ToString() => DisplayText;
}
