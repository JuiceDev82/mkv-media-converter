using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Media_MKV_Converter.Core;

public sealed class MediaConverterOptions
{
    private static readonly string DefaultToolRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "mkvtoolnix");

    public string RootPath { get; init; } = string.Empty;

    public string MkvMergePath { get; init; } = Path.Combine(DefaultToolRoot, "mkvmerge.exe");

    public string MkvPropEditPath { get; init; } = Path.Combine(DefaultToolRoot, "mkvpropedit.exe");

    public string SkipFragment { get; init; } = @"\Processing\";

    /// <summary>
    /// Specific file paths to leave out of this run, on top of <see cref="SkipFragment"/> — the GUI's
    /// preview-list checkboxes feed this. Purely additive: it can only remove files from a run, never
    /// add back ones <see cref="SkipFragment"/> already excludes. Always empty from the console, which
    /// has no per-file picker.
    /// </summary>
    public IReadOnlySet<string> ExcludedPaths { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// ISO 639 codes of the audio and subtitle languages to keep. Undetermined ("und") is always
    /// kept on top of these; see <see cref="LanguageCatalog"/>.
    /// </summary>
    public IReadOnlyList<string> Languages { get; init; } = [LanguageCatalog.DefaultCode];

    /// <summary>
    /// Audio language to flag as the default/auto-selected track when more than one is kept. Null
    /// leaves mkvmerge's own default (the first kept audio track) alone. Must be one of
    /// <see cref="Languages"/>. Only ever changes files that keep 2+ audio tracks in the first place.
    /// </summary>
    public string? PrimaryLanguage { get; init; }

    /// <summary>
    /// Drop audio tracks whose name contains "commentary", even when their language is otherwise
    /// selected — language alone can't tell a commentary track from the main one, since both
    /// typically carry the same language tag. Subtitle tracks are unaffected. Defaults to on.
    /// </summary>
    public bool ExcludeCommentaryTracks { get; init; } = true;

    /// <summary>
    /// How much to cut off the end of each file, for trailing credits. Zero keeps the whole file.
    /// mkvmerge cuts on a keyframe, so the result lands near this point rather than exactly on it.
    /// </summary>
    public TimeSpan TrimFromEnd { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Pattern whose first match truncates the output file name, dropping whatever follows it —
    /// "S01E**" turns "Show.S01E07.1080p.x265-GRP" into "Show.S01E07". Each "*" stands for exactly
    /// one character. Blank leaves names alone. See <see cref="FileNameTrimmer"/>.
    /// </summary>
    public string? TrimNameAfter { get; init; }

    /// <summary>
    /// Literal substrings to delete from the output file name — "1080p, x265, ELiTE" strips each of
    /// those wherever they occur and tidies the separator debris left behind. Applied after
    /// <see cref="TrimNameAfter"/>. Empty removes nothing. See <see cref="FileNameStringRemover"/>.
    /// </summary>
    public IReadOnlyList<string> RemoveFromName { get; init; } = [];

    /// <summary>
    /// Literal find/replace pairs applied to the output file name, in order, after
    /// <see cref="RemoveFromName"/> — "replace periods with spaces" is the motivating case. Unlike
    /// <see cref="RemoveFromName"/> this never tidies up separators afterward, since a replace is an
    /// exact substitution the user asked for, not debris left behind by deleting something. See
    /// <see cref="FileNameReplacer"/>.
    /// </summary>
    public IReadOnlyList<(string Find, string ReplaceWith)> ReplaceInName { get; init; } = [];

    public string? OutputPath { get; init; }

    public bool OverwriteExisting { get; init; } = true;

    public bool TraverseSubfolders { get; init; } = true;

    public bool MaintainFolderStructure { get; init; }

    public bool DryRun { get; init; }

    /// <summary>
    /// How many files to process at once. Defaults to 1 (sequential, the original behavior). Above
    /// 1, two things change: which of two colliding output names "wins" becomes whichever file's
    /// inspect stage finishes first rather than deterministically alphabetical, and stage log lines
    /// from different files interleave rather than running in a strict per-file block — both call
    /// out the file they belong to, so the log stays readable, just no longer strictly linear.
    /// </summary>
    public int MaxParallelFiles { get; init; } = 1;
}

public enum MediaConverterEventKind
{
    RunStarted,
    FileStarted,
    FileSkipped,
    StageChanged,
    FileCompleted,
    FileFailed,
    RunCompleted,
}

public sealed record MediaConverterProgress(
    MediaConverterEventKind Kind,
    string Message,
    string? FilePath = null,
    int FileIndex = 0,
    int TotalFiles = 0,
    decimal PercentComplete = 0,
    TimeSpan Elapsed = default,
    TimeSpan FileElapsed = default,
    string? Stage = null,
    MediaConverterRunSummary? Summary = null);

public sealed record MediaConverterRunSummary(
    int TotalFiles,
    int SuccessCount,
    int SkipCount,
    int FailureCount,
    TimeSpan Runtime);

/// <summary>One file <see cref="MediaConverterEngine.PreviewFiles"/> found under the input folder.</summary>
public sealed record FilePreviewEntry(string FullPath, bool WillBeSkipped);

public sealed class MediaConverterEngine
{
    private static readonly string[] SupportedExtensions = [".mkv", ".mp4", ".avi"];
    private static readonly string[] ForcedSubtitleKeywords = ["forced", "foreign"];
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<MediaConverterRunSummary> RunAsync(
        MediaConverterOptions options,
        IProgress<MediaConverterProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var languages = ValidateOptions(options);
        var primaryLanguage = ResolveEffectivePrimaryLanguage(options, languages);

        var totalStopwatch = Stopwatch.StartNew();
        var files = EnumerateInputFiles(options).ToList();
        var runSummaryLines = new List<string> { $"Found {files.Count} supported file(s) under {options.RootPath}" };
        runSummaryLines.Add($"Keeping languages: {string.Join(", ", languages)}");
        if (!string.IsNullOrWhiteSpace(primaryLanguage))
        {
            runSummaryLines.Add($"Primary audio language: {primaryLanguage}");
        }

        if (options.ExcludeCommentaryTracks)
        {
            runSummaryLines.Add("Excluding commentary audio tracks");
        }

        if (options.TrimFromEnd > TimeSpan.Zero)
        {
            runSummaryLines.Add($"Trimming from end: {DurationParser.Format(options.TrimFromEnd)}");
        }

        if (!string.IsNullOrWhiteSpace(options.TrimNameAfter))
        {
            runSummaryLines.Add($"Trimming names after: {options.TrimNameAfter.Trim()}");
        }

        if (options.RemoveFromName.Count > 0)
        {
            runSummaryLines.Add($"Removing from names: {string.Join(", ", options.RemoveFromName)}");
        }

        if (options.ReplaceInName.Count > 0)
        {
            // Quoted so a whitespace-only replacement (the headline use case: periods to spaces)
            // is actually visible in the log, rather than silently vanishing between commas.
            var pairText = string.Join(", ", options.ReplaceInName.Select(pair => $"'{pair.Find}' -> '{pair.ReplaceWith}'"));
            runSummaryLines.Add($"Replacing in names: {pairText}");
        }

        if (options.MaxParallelFiles > 1)
        {
            runSummaryLines.Add($"Processing up to {options.MaxParallelFiles} files at once");
        }

        progress?.Report(new MediaConverterProgress(
            MediaConverterEventKind.RunStarted,
            string.Join(Environment.NewLine, runSummaryLines),
            TotalFiles: files.Count));

        var claimedOutputPaths = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var successCount = 0;
        var skipCount = 0;
        var failureCount = 0;
        var completedCount = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.MaxParallelFiles),
            CancellationToken = cancellationToken,
        };

        // files[i] is used directly as the sequence number's source rather than a shared counter,
        // since incrementing one concurrently would need its own synchronization for no real benefit
        // — the file list's order already gives every file a stable, unique position.
        await Parallel.ForEachAsync(files.Select((path, index) => (Path: path, Sequence: index + 1)), parallelOptions, async (file, fileCancellationToken) =>
        {
            var fileStopwatch = Stopwatch.StartNew();

            // ExcludedPaths is checked separately from ShouldSkip (skip-fragment only), so
            // PreviewFiles' WillBeSkipped — computed via ShouldSkip alone — keeps meaning exactly
            // "matches the skip fragment", independent of whatever a GUI viewer has manually unchecked.
            if (ShouldSkip(file.Path, options) || options.ExcludedPaths.Contains(file.Path))
            {
                Interlocked.Increment(ref skipCount);
                var completedAt = Interlocked.Increment(ref completedCount);
                var skipPercent = CalculatePercentComplete(completedAt, files.Count);
                progress?.Report(new MediaConverterProgress(
                    MediaConverterEventKind.FileSkipped,
                    $"[{file.Sequence}/{files.Count}] [skip] {file.Path} ({skipPercent:0.#}%, elapsed {FormatDuration(totalStopwatch.Elapsed)})",
                    file.Path,
                    file.Sequence,
                    files.Count,
                    skipPercent,
                    totalStopwatch.Elapsed,
                    fileStopwatch.Elapsed));
                return;
            }

            try
            {
                // A best-effort snapshot for the "starting percent" shown in the FileStarted event —
                // under concurrency this is approximate (another file may finish a moment later), same
                // as any other live progress percentage; it is display-only, never a control-flow value.
                await ProcessFileAsync(file.Path, file.Sequence, completedCount, files.Count, totalStopwatch, fileStopwatch, options, languages, primaryLanguage, claimedOutputPaths, progress, fileCancellationToken);
                Interlocked.Increment(ref successCount);
                Interlocked.Increment(ref completedCount);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failureCount);
                var completedAt = Interlocked.Increment(ref completedCount);
                var failurePercent = CalculatePercentComplete(completedAt, files.Count);
                progress?.Report(new MediaConverterProgress(
                    MediaConverterEventKind.FileFailed,
                    $"[error] {file.Path}{Environment.NewLine}        File runtime: {FormatDuration(fileStopwatch.Elapsed)}{Environment.NewLine}        {ex.Message}",
                    file.Path,
                    file.Sequence,
                    files.Count,
                    failurePercent,
                    totalStopwatch.Elapsed,
                    fileStopwatch.Elapsed));
            }
        });

        var summary = new MediaConverterRunSummary(
            files.Count,
            successCount,
            skipCount,
            failureCount,
            totalStopwatch.Elapsed);

        progress?.Report(new MediaConverterProgress(
            MediaConverterEventKind.RunCompleted,
            $"Summary{Environment.NewLine}  Success: {summary.SuccessCount}{Environment.NewLine}  Skipped: {summary.SkipCount}{Environment.NewLine}  Failed : {summary.FailureCount}{Environment.NewLine}  Runtime: {FormatDuration(summary.Runtime)}",
            TotalFiles: files.Count,
            Elapsed: summary.Runtime,
            Summary: summary));

        return summary;
    }

    /// <summary>
    /// Lists the files a run would find under <see cref="MediaConverterOptions.RootPath"/>, without
    /// touching anything. Reuses <see cref="RunAsync"/>'s own enumeration and skip check, so this
    /// stays accurate as that logic changes — a GUI preview built by re-implementing the rule
    /// separately would quietly drift from it. Returns an empty list rather than throwing when the
    /// root path is blank or does not exist yet, since callers typically use this while a folder
    /// path is still being typed or browsed.
    /// </summary>
    public IReadOnlyList<FilePreviewEntry> PreviewFiles(MediaConverterOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RootPath) || !Directory.Exists(options.RootPath))
        {
            return [];
        }

        return EnumerateInputFiles(options)
            .Select(path => new FilePreviewEntry(path, ShouldSkip(path, options)))
            .ToList();
    }

    private IEnumerable<string> EnumerateInputFiles(MediaConverterOptions options)
    {
        var searchOption = options.TraverseSubfolders
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        return Directory.EnumerateFiles(options.RootPath, "*.*", searchOption)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static bool ShouldSkip(string filePath, MediaConverterOptions options)
    {
        return filePath.Contains(options.SkipFragment, StringComparison.OrdinalIgnoreCase);
    }

    private async Task ProcessFileAsync(
        string inputPath,
        int fileIndex,
        int completedCountBefore,
        int totalFiles,
        Stopwatch totalStopwatch,
        Stopwatch fileStopwatch,
        MediaConverterOptions options,
        IReadOnlyList<string> languages,
        string? primaryLanguage,
        ConcurrentDictionary<string, byte> claimedOutputPaths,
        IProgress<MediaConverterProgress>? progress,
        CancellationToken cancellationToken)
    {
        var startPercent = CalculatePercentComplete(completedCountBefore, totalFiles);
        progress?.Report(new MediaConverterProgress(
            MediaConverterEventKind.FileStarted,
            $"[{fileIndex}/{totalFiles}] [work] {inputPath} (elapsed {FormatDuration(totalStopwatch.Elapsed)})",
            inputPath,
            fileIndex,
            totalFiles,
            startPercent,
            totalStopwatch.Elapsed,
            fileStopwatch.Elapsed));

        if (options.DryRun)
        {
            ReportStage("inspect-source", 1, 2);
        }
        else
        {
            ReportStage("inspect-source", 1, 9);
        }

        var originalInfo = await ReadMkvInfoAsync(inputPath, options, cancellationToken);
        EnsureAudioSurvives(originalInfo, languages, options.ExcludeCommentaryTracks);
        var trimEndpoint = ResolveTrimEndpoint(originalInfo, options);
        var forcedSubtitlePositions = GetForcedSubtitlePositions(originalInfo, languages);
        var preferredOutputPath = GetPreferredOutputPath(inputPath, options);

        // Trimming names makes collisions easy — two releases of one episode reduce to the same
        // name — and in-place mode would delete the loser's source after overwriting its output.
        // TryAdd rather than Add: under parallel processing, two files can reach this line at once,
        // and only a genuinely atomic check-and-claim avoids a race where both believe they won.
        if (!claimedOutputPaths.TryAdd(preferredOutputPath, 0))
        {
            throw new InvalidOperationException(
                $"Another file in this run already produces \"{Path.GetFileName(preferredOutputPath)}\". " +
                "Left unchanged so that one is not overwritten.");
        }

        var tempOutputPath = GetTempOutputPath(preferredOutputPath);

        if (options.DryRun)
        {
            ReportStage("dry-run", 2, 2);
            progress?.Report(new MediaConverterProgress(
                MediaConverterEventKind.FileCompleted,
                $"        Output: {ResolveOutputPath(preferredOutputPath, options)}{Environment.NewLine}        File runtime: {FormatDuration(fileStopwatch.Elapsed)}{Environment.NewLine}        Forced subtitle positions: {FormatList(forcedSubtitlePositions)}{DescribeTrimPlan(originalInfo, trimEndpoint)}{DescribeRename(inputPath, ResolveOutputPath(preferredOutputPath, options))}{DescribeCommentaryExclusionPlan(originalInfo, languages, options.ExcludeCommentaryTracks)}{DescribePrimaryAudioPlan(originalInfo, languages, options.ExcludeCommentaryTracks, primaryLanguage)}{Environment.NewLine}        Would clear track names after remux",
                inputPath,
                fileIndex,
                totalFiles,
                CalculatePercentComplete(completedCountBefore + 1, totalFiles),
                totalStopwatch.Elapsed,
                fileStopwatch.Elapsed));
            return;
        }

        DeleteIfExists(tempOutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(preferredOutputPath) ?? options.RootPath);

        try
        {
            ReportStage("remux", 2, 9);
            await RunProcessAsync(
                options.MkvMergePath,
                BuildMkvMergeArguments(inputPath, tempOutputPath, originalInfo, languages, options.ExcludeCommentaryTracks, trimEndpoint),
                "mkvmerge failed",
                cancellationToken);

            if (!File.Exists(tempOutputPath))
            {
                throw new InvalidOperationException($"mkvmerge did not create output file: {tempOutputPath}");
            }

            ReportStage("replace-output", 3, 9);
            var finalOutputPath = ReplaceOutput(inputPath, preferredOutputPath, tempOutputPath, options);

            ReportStage("inspect-output", 4, 9);
            var outputInfo = await ReadMkvInfoAsync(finalOutputPath, options, cancellationToken);
            var namedTrackPositions = GetNamedTrackPositions(outputInfo);
            var forcedNamedTrackPositions = forcedSubtitlePositions
                .Select(position => TryGetTrackPositionForSubtitle(outputInfo, position))
                .Where(position => position.HasValue)
                .Select(position => position!.Value)
                .ToHashSet();

            ReportStage("set-forced-flags", 5, 9);
            foreach (var subtitlePosition in forcedSubtitlePositions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunProcessAsync(
                    options.MkvPropEditPath,
                    $"\"{finalOutputPath}\" --edit track:s{subtitlePosition} --set flag-forced=1",
                    $"Failed to set forced flag on subtitle track {subtitlePosition}",
                    cancellationToken);
            }

            ReportStage("clear-track-names", 6, 9);
            foreach (var trackPosition in namedTrackPositions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (forcedNamedTrackPositions.Contains(trackPosition))
                {
                    continue;
                }

                await RunProcessAsync(
                    options.MkvPropEditPath,
                    $"\"{finalOutputPath}\" --edit track:{trackPosition} --set name=",
                    $"Failed to clear track name on track {trackPosition}",
                    cancellationToken);
            }

            ReportStage("set-forced-names", 7, 9);
            foreach (var subtitlePosition in forcedSubtitlePositions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunProcessAsync(
                    options.MkvPropEditPath,
                    $"\"{finalOutputPath}\" --edit track:s{subtitlePosition} --set name=Forced",
                    $"Failed to set track name to Forced on subtitle track {subtitlePosition}",
                    cancellationToken);
            }

            ReportStage("set-primary-audio", 8, 9);
            var primaryAudioFlags = ResolvePrimaryAudioTrackFlags(outputInfo, primaryLanguage);
            var primaryAudioPosition = primaryAudioFlags?.FirstOrDefault(f => f.IsDefault).Position;
            if (primaryAudioFlags is not null)
            {
                foreach (var (position, isDefault) in primaryAudioFlags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await RunProcessAsync(
                        options.MkvPropEditPath,
                        $"\"{finalOutputPath}\" --edit track:{position} --set flag-default={(isDefault ? 1 : 0)}",
                        $"Failed to set default flag on audio track {position}",
                        cancellationToken);
                }
            }

            ReportStage("complete", 9, 9);
            progress?.Report(new MediaConverterProgress(
                MediaConverterEventKind.FileCompleted,
                $"        Output: {finalOutputPath}{Environment.NewLine}        File runtime: {FormatDuration(fileStopwatch.Elapsed)}{Environment.NewLine}        Forced subtitle positions: {FormatList(forcedSubtitlePositions)}{DescribeTrimResult(originalInfo, outputInfo, trimEndpoint)}{DescribeRename(inputPath, finalOutputPath)}{DescribePrimaryAudio(primaryAudioPosition)}{Environment.NewLine}        Cleared track names: {FormatList(namedTrackPositions.Where(position => !forcedNamedTrackPositions.Contains(position)).ToList())}",
                inputPath,
                fileIndex,
                totalFiles,
                CalculatePercentComplete(completedCountBefore + 1, totalFiles),
                totalStopwatch.Elapsed,
                fileStopwatch.Elapsed));
        }
        finally
        {
            DeleteIfExists(tempOutputPath);
        }

        void ReportStage(string stage, int currentStep, int totalSteps)
        {
            var filePercent = totalSteps <= 0
                ? 0m
                : decimal.Round(currentStep * 100m / totalSteps, 0, MidpointRounding.AwayFromZero);

            progress?.Report(new MediaConverterProgress(
                MediaConverterEventKind.StageChanged,
                // Named explicitly rather than relying on position under the preceding FileStarted
                // line: with MaxParallelFiles > 1, stage lines from different files interleave.
                $"        [{Path.GetFileName(inputPath)}] -> {stage} ({filePercent:0}%)",
                inputPath,
                fileIndex,
                totalFiles,
                startPercent,
                totalStopwatch.Elapsed,
                fileStopwatch.Elapsed,
                stage));
        }
    }

    /// <summary>
    /// The output file name <see cref="TrimNameAfter"/>, <see cref="RemoveFromName"/> and
    /// <see cref="ReplaceInName"/> would give a source file, with its <c>.mkv</c> extension — the
    /// renaming half of <see cref="GetPreferredOutputPath"/>, factored out so a caller that only
    /// wants a name preview (the GUI file list, for instance) uses the exact same logic a real run
    /// would, rather than a second implementation that could quietly drift from it. Applied in that
    /// order — trim, then remove, then replace — so a trim or remove pattern always sees the
    /// original punctuation rather than whatever a replace already changed it to. Invalid patterns
    /// are not rejected here — that is <c>ValidateOptions</c>' job before a run starts — so a caller
    /// previewing live, in-progress typing should check <see cref="FileNameTrimmer.IsValidPattern"/>
    /// itself first if showing a result for a pattern that could never actually run would be misleading.
    /// </summary>
    public static string PreviewOutputFileName(
        string fileNameWithoutExtension,
        string? trimNameAfter,
        IReadOnlyList<string>? removeFromName,
        IReadOnlyList<(string Find, string ReplaceWith)>? replaceInName)
    {
        var trimmedName = FileNameTrimmer.Trim(fileNameWithoutExtension, trimNameAfter);
        var removedName = FileNameStringRemover.Remove(trimmedName, removeFromName ?? []);
        return FileNameReplacer.Replace(removedName, replaceInName ?? []) + ".mkv";
    }

    private static string GetPreferredOutputPath(string inputPath, MediaConverterOptions options)
    {
        var outputFileName = PreviewOutputFileName(Path.GetFileNameWithoutExtension(inputPath), options.TrimNameAfter, options.RemoveFromName, options.ReplaceInName);

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            var sourceDirectory = Path.GetDirectoryName(inputPath);
            return string.IsNullOrEmpty(sourceDirectory)
                ? outputFileName
                : Path.Combine(sourceDirectory, outputFileName);
        }

        return options.MaintainFolderStructure
            ? Path.Combine(options.OutputPath, GetRelativeDirectory(inputPath, options.RootPath), outputFileName)
            : Path.Combine(options.OutputPath, outputFileName);
    }

    private static string GetRelativeDirectory(string inputPath, string rootPath)
    {
        var sourceDirectory = Path.GetDirectoryName(inputPath);
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return string.Empty;
        }

        var relativeDirectory = Path.GetRelativePath(rootPath, sourceDirectory);
        if (relativeDirectory == "."
            || Path.IsPathRooted(relativeDirectory)
            || relativeDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
        {
            return string.Empty;
        }

        return relativeDirectory;
    }

    private static string GetTempOutputPath(string preferredOutputPath)
    {
        var directory = Path.GetDirectoryName(preferredOutputPath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(preferredOutputPath);
        return Path.Combine(directory, $"{fileNameWithoutExtension}.tmp-{Guid.NewGuid():N}.mkv");
    }

    private static string ResolveOutputPath(string preferredOutputPath, MediaConverterOptions options)
    {
        if (options.OverwriteExisting)
        {
            return preferredOutputPath;
        }

        return GetUniqueOutputPath(preferredOutputPath);
    }

    private static string ReplaceOutput(string inputPath, string preferredOutputPath, string tempOutputPath, MediaConverterOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            var destinationPath = ResolveOutputPath(preferredOutputPath, options);
            if (options.OverwriteExisting)
            {
                DeleteIfExists(destinationPath);
            }
            File.Move(tempOutputPath, destinationPath);
            return destinationPath;
        }

        var finalOutputPath = ResolveOutputPath(preferredOutputPath, options);
        if (string.Equals(finalOutputPath, inputPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(inputPath);
            File.Move(tempOutputPath, finalOutputPath);
            return finalOutputPath;
        }

        if (options.OverwriteExisting)
        {
            DeleteIfExists(finalOutputPath);
        }

        File.Move(tempOutputPath, finalOutputPath);

        if (options.OverwriteExisting && !string.Equals(inputPath, finalOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(inputPath);
        }

        return finalOutputPath;
    }

    private static string GetUniqueOutputPath(string desiredPath)
    {
        if (!File.Exists(desiredPath))
        {
            return desiredPath;
        }

        var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);

        for (var index = 1; ; index++)
        {
            var candidate = Path.Combine(directory, $"{fileNameWithoutExtension} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private async Task<MkvInfo> ReadMkvInfoAsync(string filePath, MediaConverterOptions options, CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
            options.MkvMergePath,
            $"-J \"{filePath}\"",
            "mkvmerge JSON inspection failed",
            cancellationToken);

        var info = JsonSerializer.Deserialize<MkvInfo>(result.StandardOutput, _jsonOptions);
        if (info?.Tracks is null)
        {
            throw new InvalidOperationException($"Unable to parse mkvmerge JSON for {filePath}");
        }

        return info;
    }

    private static List<int> GetForcedSubtitlePositions(MkvInfo info, IReadOnlyList<string> languages)
    {
        var positions = new List<int>();
        var subtitlePosition = 0;

        foreach (var track in info.Tracks)
        {
            if (!ShouldIncludeSubtitleTrack(track, languages))
            {
                continue;
            }

            subtitlePosition++;
            var hasForcedKeyword =
                !string.IsNullOrWhiteSpace(track.Properties.TrackName) &&
                ForcedSubtitleKeywords.Any(keyword => track.Properties.TrackName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            if (hasForcedKeyword || track.Properties.ForcedTrack)
            {
                positions.Add(subtitlePosition);
            }
        }

        return positions;
    }

    /// <summary>
    /// Mirrors what <c>--subtitle-tracks</c> will keep, so subtitle-relative forced positions taken
    /// from the source still line up with the remuxed output. Untagged tracks survive because
    /// <c>--default-language</c> stamps one on during the remux.
    /// </summary>
    private static bool ShouldIncludeSubtitleTrack(MkvTrack track, IReadOnlyList<string> languages) =>
        IsTrackType(track, "subtitles") && IsLanguageKept(track, languages);

    private static bool IsTrackType(MkvTrack track, string type) =>
        string.Equals(track.Type, type, StringComparison.OrdinalIgnoreCase);

    private static bool IsLanguageKept(MkvTrack track, IReadOnlyList<string> languages)
    {
        var language = track.Properties.Language;
        return string.IsNullOrWhiteSpace(language) || LanguageCatalog.IsSelected(languages, language);
    }

    private static bool IsCommentaryTrack(MkvTrack track) =>
        !string.IsNullOrWhiteSpace(track.Properties.TrackName) &&
        track.Properties.TrackName.Contains("commentary", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Audio tracks that will actually survive the remux: language-selected, and — when
    /// <paramref name="excludeCommentary"/> — not name-flagged as commentary. The single source of
    /// truth for "what audio makes it through", shared by <see cref="EnsureAudioSurvives"/> and
    /// <see cref="BuildMkvMergeArguments"/> so the two can never disagree about which tracks these
    /// are, the same discipline <see cref="LanguageCatalog"/> already requires for languages.
    /// </summary>
    private static List<MkvTrack> GetSurvivingAudioTracks(MkvInfo info, IReadOnlyList<string> languages, bool excludeCommentary) =>
        info.Tracks
            .Where(track => IsTrackType(track, "audio"))
            .Where(track => IsLanguageKept(track, languages))
            .Where(track => !excludeCommentary || !IsCommentaryTrack(track))
            .ToList();

    /// <summary>
    /// Guards against remuxing away every audio track. In-place mode deletes the source, so a
    /// selection the file cannot satisfy would otherwise destroy the only copy.
    /// </summary>
    private static void EnsureAudioSurvives(MkvInfo info, IReadOnlyList<string> languages, bool excludeCommentary)
    {
        var audioTracks = info.Tracks.Where(track => IsTrackType(track, "audio")).ToList();
        if (audioTracks.Count == 0 || GetSurvivingAudioTracks(info, languages, excludeCommentary).Count > 0)
        {
            return;
        }

        var languageMatches = audioTracks.Where(track => IsLanguageKept(track, languages)).ToList();

        if (languageMatches.Count > 0)
        {
            // Every matching track exists but is commentary-flagged — a different, more useful
            // message than "no audio in these languages", since audio in that language does exist.
            throw new InvalidOperationException(
                $"Every audio track in the selected languages ({string.Join(", ", languages)}) is a commentary " +
                "track, and commentary tracks are excluded. Left unchanged. Turn off commentary exclusion to keep it anyway.");
        }

        var available = audioTracks
            .Select(track => string.IsNullOrWhiteSpace(track.Properties.Language) ? "und" : track.Properties.Language!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);

        throw new InvalidOperationException(
            $"No audio track matches the selected languages ({string.Join(", ", languages)}). " +
            $"This file only has: {string.Join(", ", available)}. " +
            "Left unchanged so it keeps its audio.");
    }

    private static List<int> GetNamedTrackPositions(MkvInfo info)
    {
        var positions = new List<int>();

        for (var index = 0; index < info.Tracks.Count; index++)
        {
            var track = info.Tracks[index];
            if (!string.IsNullOrWhiteSpace(track.Properties.TrackName))
            {
                positions.Add(index + 1);
            }
        }

        return positions;
    }

    /// <summary>
    /// Absolute track positions (1-based, matching mkvpropedit's <c>track:N</c> addressing) of every
    /// audio track in the output, paired with whether each should become the default. Returns null
    /// — meaning "leave mkvmerge's own default choice alone" — only when there is nothing to do: no
    /// primary language set, or the requested language was not actually kept in this particular file.
    /// Runs even with a single surviving audio track: mkvmerge copies each kept track's own
    /// default-track flag from the source rather than recomputing it, so if the file's *original*
    /// default was a different, now-stripped track, the lone survivor can come out of the remux
    /// still flagged non-default even though it is the only audio choice left. Explicitly setting the
    /// primary-language match's flag (and clearing every other audio track's) is what fixes that.
    /// </summary>
    private static List<(int Position, bool IsDefault)>? ResolvePrimaryAudioTrackFlags(MkvInfo outputInfo, string? primaryLanguage)
    {
        if (string.IsNullOrWhiteSpace(primaryLanguage))
        {
            return null;
        }

        var audioTracks = outputInfo.Tracks
            .Select((track, index) => (Track: track, Position: index + 1))
            .Where(t => IsTrackType(t.Track, "audio"))
            .ToList();

        if (audioTracks.Count == 0)
        {
            return null;
        }

        var primaryList = new[] { primaryLanguage };
        var winnerPosition = audioTracks
            .Where(t => !string.IsNullOrWhiteSpace(t.Track.Properties.Language) &&
                        LanguageCatalog.IsSelected(primaryList, t.Track.Properties.Language!))
            .Select(t => (int?)t.Position)
            .FirstOrDefault();

        if (winnerPosition is null)
        {
            return null;
        }

        return audioTracks.Select(t => (t.Position, IsDefault: t.Position == winnerPosition)).ToList();
    }

    private static int? TryGetTrackPositionForSubtitle(MkvInfo info, int subtitlePosition)
    {
        var currentSubtitlePosition = 0;

        for (var index = 0; index < info.Tracks.Count; index++)
        {
            if (!string.Equals(info.Tracks[index].Type, "subtitles", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            currentSubtitlePosition++;
            if (currentSubtitlePosition == subtitlePosition)
            {
                return index + 1;
            }
        }

        return null;
    }

    private static string BuildMkvMergeArguments(
        string inputPath,
        string outputPath,
        MkvInfo sourceInfo,
        IReadOnlyList<string> languages,
        bool excludeCommentary,
        TimeSpan? trimEndpoint)
    {
        var trackLanguages = string.Join(',', languages);

        // Commentary tracks share their language with the "real" track, so language alone can't
        // exclude them — mkvmerge's --audio-tracks flag has to fall back to explicit source track
        // IDs instead of the simple language list for this file. Only done when a commentary track
        // is actually present among the language matches, so the common case (no commentary in the
        // file, or the option off) keeps using the plain, well-exercised language-list form.
        var audioSelector = trackLanguages;
        if (excludeCommentary)
        {
            var languageMatches = GetSurvivingAudioTracks(sourceInfo, languages, excludeCommentary: false);
            if (languageMatches.Any(IsCommentaryTrack))
            {
                var surviving = GetSurvivingAudioTracks(sourceInfo, languages, excludeCommentary: true);
                audioSelector = string.Join(',', surviving.Select(track => track.Id));
            }
        }

        var arguments = new List<string>
        {
            "--output",
            $"\"{outputPath}\"",
            "--audio-tracks",
            audioSelector,
            "--subtitle-tracks",
            trackLanguages,
            "--default-language",
            LanguageCatalog.GetDefaultLanguage(languages),
            "--title",
            "\"\"",
            "--no-global-tags",
            "--no-track-tags",
            "--no-attachments",
            "--no-chapters",
        };

        // A single "parts" range keeps one span and writes it to the given output name, with none
        // of the numeric suffixing mkvmerge applies when a split produces several files.
        if (trimEndpoint is { } endpoint)
        {
            arguments.Add("--split");
            arguments.Add($"parts:00:00:00-{FormatTimestamp(endpoint)}");
        }

        arguments.Add($"\"{inputPath}\"");

        return string.Join(' ', arguments);
    }

    private static string FormatTimestamp(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds:000}";

    /// <summary>
    /// Where the remux should stop, or null when nothing is being trimmed. Throws rather than
    /// guessing, because in-place mode deletes the source once the remux succeeds.
    /// </summary>
    private static TimeSpan? ResolveTrimEndpoint(MkvInfo info, MediaConverterOptions options)
    {
        if (options.TrimFromEnd <= TimeSpan.Zero)
        {
            return null;
        }

        var duration = info.GetDuration();
        if (duration is null)
        {
            throw new InvalidOperationException(
                "Cannot trim the end: mkvmerge did not report a duration for this file.");
        }

        var endpoint = duration.Value - options.TrimFromEnd;
        if (endpoint <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"Cannot trim {DurationParser.Format(options.TrimFromEnd)} from the end: " +
                $"this file is only {FormatDuration(duration.Value)} long. Left unchanged.");
        }

        return endpoint;
    }

    private static string DescribeRename(string inputPath, string outputPath)
    {
        var before = Path.GetFileName(inputPath);
        var after = Path.GetFileName(outputPath);

        return string.Equals(before, after, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"{Environment.NewLine}        Renamed: {before} -> {after}";
    }

    private static string DescribePrimaryAudio(int? position) =>
        position is null ? string.Empty : $"{Environment.NewLine}        Default audio track: {position}";

    private static string DescribeCommentaryExclusionPlan(MkvInfo source, IReadOnlyList<string> languages, bool excludeCommentary)
    {
        if (!excludeCommentary)
        {
            return string.Empty;
        }

        var excludedCount = GetSurvivingAudioTracks(source, languages, excludeCommentary: false).Count(IsCommentaryTrack);
        return excludedCount == 0
            ? string.Empty
            : $"{Environment.NewLine}        Would exclude {excludedCount} commentary audio track(s)";
    }

    private static string DescribePrimaryAudioPlan(MkvInfo source, IReadOnlyList<string> languages, bool excludeCommentary, string? primaryLanguage)
    {
        if (string.IsNullOrWhiteSpace(primaryLanguage))
        {
            return string.Empty;
        }

        var surviving = GetSurvivingAudioTracks(source, languages, excludeCommentary);
        var primaryList = new[] { primaryLanguage };
        var hasMatch = surviving.Any(t =>
            !string.IsNullOrWhiteSpace(t.Properties.Language) && LanguageCatalog.IsSelected(primaryList, t.Properties.Language!));

        return hasMatch
            ? $"{Environment.NewLine}        Would set default audio track to {primaryLanguage}"
            : string.Empty;
    }

    /// <summary>Dry-run wording. The endpoint is approximate because mkvmerge snaps it to a keyframe.</summary>
    private static string DescribeTrimPlan(MkvInfo source, TimeSpan? trimEndpoint)
    {
        if (trimEndpoint is not { } endpoint)
        {
            return string.Empty;
        }

        var from = source.GetDuration();
        var was = from is null ? string.Empty : $" (currently {FormatDuration(from.Value)})";
        return $"{Environment.NewLine}        Would trim to about {FormatDuration(endpoint)}{was}";
    }

    /// <summary>
    /// Reports what the trim actually produced rather than what was asked for: mkvmerge cuts on the
    /// next keyframe at or after the requested point, so the result runs slightly long.
    /// </summary>
    private static string DescribeTrimResult(MkvInfo source, MkvInfo output, TimeSpan? trimEndpoint)
    {
        if (trimEndpoint is null)
        {
            return string.Empty;
        }

        var before = source.GetDuration();
        var after = output.GetDuration();

        if (before is null || after is null)
        {
            return $"{Environment.NewLine}        Trimmed the end";
        }

        return $"{Environment.NewLine}        Trimmed: {FormatDuration(before.Value)} -> {FormatDuration(after.Value)}";
    }

    private static string FormatList(IReadOnlyCollection<int> values)
    {
        return values.Count == 0 ? "(none)" : string.Join(", ", values);
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return duration.ToString(@"h\:mm\:ss");
        }

        return duration.ToString(@"m\:ss");
    }

    private static decimal CalculatePercentComplete(int completedCount, int totalFiles)
    {
        if (totalFiles == 0)
        {
            return 100m;
        }

        return decimal.Round(completedCount * 100m / totalFiles, 1, MidpointRounding.AwayFromZero);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates the options and returns the language codes the run will keep.</summary>
    private static IReadOnlyList<string> ValidateOptions(MediaConverterOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            throw new ArgumentException("Missing root path.");
        }

        if (!Directory.Exists(options.RootPath))
        {
            throw new ArgumentException($"Root path does not exist: {options.RootPath}");
        }

        if (!File.Exists(options.MkvMergePath))
        {
            throw new ArgumentException($"mkvmerge.exe not found: {options.MkvMergePath}");
        }

        if (!File.Exists(options.MkvPropEditPath))
        {
            throw new ArgumentException($"mkvpropedit.exe not found: {options.MkvPropEditPath}");
        }

        if (options.TrimFromEnd < TimeSpan.Zero)
        {
            throw new ArgumentException("Trim from end cannot be negative.");
        }

        if (options.MaxParallelFiles < 1)
        {
            throw new ArgumentException("Parallel files must be at least 1.");
        }

        if (!FileNameTrimmer.IsValidPattern(options.TrimNameAfter))
        {
            throw new ArgumentException(
                "The name pattern needs at least one ordinary character; a pattern of only '*' would match every name at its start.");
        }

        foreach (var code in options.Languages ?? [])
        {
            if (!string.IsNullOrWhiteSpace(code) && !LanguageCatalog.IsValidCode(code.Trim()))
            {
                throw new ArgumentException(
                    $"Invalid language code: '{code}'. Use ISO 639 codes such as eng, spa or fre.");
            }
        }

        var normalizedLanguages = LanguageCatalog.Normalize(options.Languages);

        if (!string.IsNullOrWhiteSpace(options.PrimaryLanguage))
        {
            var primaryLanguage = options.PrimaryLanguage.Trim();
            if (!LanguageCatalog.IsValidCode(primaryLanguage))
            {
                throw new ArgumentException(
                    $"Invalid primary language code: '{options.PrimaryLanguage}'. Use an ISO 639 code such as eng, spa or fre.");
            }

            if (!LanguageCatalog.IsSelected(normalizedLanguages, primaryLanguage))
            {
                throw new ArgumentException(
                    $"Primary language '{primaryLanguage}' must also be one of the selected languages to keep.");
            }
        }

        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            Directory.CreateDirectory(options.OutputPath);
        }

        return normalizedLanguages;
    }

    /// <summary>
    /// The primary language a run actually uses: the explicit choice when there is one (already
    /// validated to be one of <paramref name="languages"/>), otherwise English if English is itself
    /// among the kept languages. Deliberately not a static default on <see cref="MediaConverterOptions.PrimaryLanguage"/>
    /// itself — baking "eng" in there would fail validation outright for anyone keeping languages
    /// that don't include English, for a default they never asked to opt into. Leaving English out
    /// of a language selection on purpose — a foreign-only library — correctly yields no implicit
    /// primary language, rather than guessing at some other one.
    /// </summary>
    private static string? ResolveEffectivePrimaryLanguage(MediaConverterOptions options, IReadOnlyList<string> languages)
    {
        if (!string.IsNullOrWhiteSpace(options.PrimaryLanguage))
        {
            return options.PrimaryLanguage.Trim();
        }

        return LanguageCatalog.IsSelected(languages, LanguageCatalog.DefaultCode) ? LanguageCatalog.DefaultCode : null;
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        string arguments,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch
            {
            }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"{errorMessage}: {details.Trim()}");
        }

        return new ProcessResult(stdout, stderr);
    }
}

internal sealed record ProcessResult(string StandardOutput, string StandardError);

internal sealed class MkvInfo
{
    [JsonPropertyName("container")]
    public MkvContainer Container { get; init; } = new();

    [JsonPropertyName("tracks")]
    public List<MkvTrack> Tracks { get; init; } = [];

    /// <summary>Source duration, or null when mkvmerge could not determine one.</summary>
    public TimeSpan? GetDuration() =>
        Container.Properties.Duration is > 0 and var nanoseconds
            ? TimeSpan.FromTicks(nanoseconds / 100)
            : null;
}

internal sealed class MkvContainer
{
    [JsonPropertyName("properties")]
    public MkvContainerProperties Properties { get; init; } = new();
}

internal sealed class MkvContainerProperties
{
    /// <summary>Duration in nanoseconds. Absent for some containers and for damaged files.</summary>
    [JsonPropertyName("duration")]
    public long? Duration { get; init; }
}

internal sealed class MkvTrack
{
    /// <summary>mkvmerge's own numeric track ID — what <c>--audio-tracks</c> takes for explicit
    /// (non-language) track selection. Distinct from every "position" used elsewhere in this file,
    /// which are 1-based indexes into the track list, not this ID.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("properties")]
    public MkvTrackProperties Properties { get; init; } = new();
}

internal sealed class MkvTrackProperties
{
    [JsonPropertyName("track_name")]
    public string? TrackName { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("forced_track")]
    public bool ForcedTrack { get; init; }
}
