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

    public string? OutputPath { get; init; }

    public bool OverwriteExisting { get; init; } = true;

    public bool TraverseSubfolders { get; init; } = true;

    public bool MaintainFolderStructure { get; init; }

    public bool DryRun { get; init; }
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

public sealed class MediaConverterEngine
{
    private static readonly string[] SupportedExtensions = [".mkv", ".mp4", ".avi"];
    private static readonly string[] ForcedSubtitleKeywords = ["forced", "foreign"];
    private static readonly string[] SelectedLanguages = ["eng", "und"];
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<MediaConverterRunSummary> RunAsync(
        MediaConverterOptions options,
        IProgress<MediaConverterProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        var totalStopwatch = Stopwatch.StartNew();
        var files = EnumerateInputFiles(options).ToList();
        progress?.Report(new MediaConverterProgress(
            MediaConverterEventKind.RunStarted,
            $"Found {files.Count} supported file(s) under {options.RootPath}",
            TotalFiles: files.Count));

        var successCount = 0;
        var skipCount = 0;
        var failureCount = 0;
        var completedCount = 0;
        var sequenceIndex = 0;

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            sequenceIndex++;
            var fileStopwatch = Stopwatch.StartNew();

            if (ShouldSkip(filePath, options))
            {
                skipCount++;
                completedCount++;
                var skipPercent = CalculatePercentComplete(completedCount, files.Count);
                progress?.Report(new MediaConverterProgress(
                    MediaConverterEventKind.FileSkipped,
                    $"[{sequenceIndex}/{files.Count}] [skip] {filePath} ({skipPercent:0.#}%, elapsed {FormatDuration(totalStopwatch.Elapsed)})",
                    filePath,
                    sequenceIndex,
                    files.Count,
                    skipPercent,
                    totalStopwatch.Elapsed,
                    fileStopwatch.Elapsed));
                continue;
            }

            try
            {
                await ProcessFileAsync(filePath, sequenceIndex, completedCount, files.Count, totalStopwatch, fileStopwatch, options, progress, cancellationToken);
                successCount++;
                completedCount++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failureCount++;
                completedCount++;
                var failurePercent = CalculatePercentComplete(completedCount, files.Count);
                progress?.Report(new MediaConverterProgress(
                    MediaConverterEventKind.FileFailed,
                    $"[error] {filePath}{Environment.NewLine}        File runtime: {FormatDuration(fileStopwatch.Elapsed)}{Environment.NewLine}        {ex.Message}",
                    filePath,
                    sequenceIndex,
                    files.Count,
                    failurePercent,
                    totalStopwatch.Elapsed,
                    fileStopwatch.Elapsed));
            }
        }

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
            ReportStage("inspect-source", 1, 8);
        }

        var originalInfo = await ReadMkvInfoAsync(inputPath, options, cancellationToken);
        var forcedSubtitlePositions = GetForcedSubtitlePositions(originalInfo);
        var preferredOutputPath = GetPreferredOutputPath(inputPath, options);
        var tempOutputPath = GetTempOutputPath(preferredOutputPath);

        if (options.DryRun)
        {
            ReportStage("dry-run", 2, 2);
            progress?.Report(new MediaConverterProgress(
                MediaConverterEventKind.FileCompleted,
                $"        Output: {ResolveOutputPath(preferredOutputPath, options)}{Environment.NewLine}        File runtime: {FormatDuration(fileStopwatch.Elapsed)}{Environment.NewLine}        Forced subtitle positions: {FormatList(forcedSubtitlePositions)}{Environment.NewLine}        Would clear track names after remux",
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
            ReportStage("remux", 2, 8);
            await RunProcessAsync(
                options.MkvMergePath,
                BuildMkvMergeArguments(inputPath, tempOutputPath),
                "mkvmerge failed",
                cancellationToken);

            if (!File.Exists(tempOutputPath))
            {
                throw new InvalidOperationException($"mkvmerge did not create output file: {tempOutputPath}");
            }

            ReportStage("replace-output", 3, 8);
            var finalOutputPath = ReplaceOutput(inputPath, preferredOutputPath, tempOutputPath, options);

            ReportStage("inspect-output", 4, 8);
            var outputInfo = await ReadMkvInfoAsync(finalOutputPath, options, cancellationToken);
            var namedTrackPositions = GetNamedTrackPositions(outputInfo);
            var forcedNamedTrackPositions = forcedSubtitlePositions
                .Select(position => TryGetTrackPositionForSubtitle(outputInfo, position))
                .Where(position => position.HasValue)
                .Select(position => position!.Value)
                .ToHashSet();

            ReportStage("set-forced-flags", 5, 8);
            foreach (var subtitlePosition in forcedSubtitlePositions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunProcessAsync(
                    options.MkvPropEditPath,
                    $"\"{finalOutputPath}\" --edit track:s{subtitlePosition} --set flag-forced=1",
                    $"Failed to set forced flag on subtitle track {subtitlePosition}",
                    cancellationToken);
            }

            ReportStage("clear-track-names", 6, 8);
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

            ReportStage("set-forced-names", 7, 8);
            foreach (var subtitlePosition in forcedSubtitlePositions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunProcessAsync(
                    options.MkvPropEditPath,
                    $"\"{finalOutputPath}\" --edit track:s{subtitlePosition} --set name=Forced",
                    $"Failed to set track name to Forced on subtitle track {subtitlePosition}",
                    cancellationToken);
            }

            ReportStage("complete", 8, 8);
            progress?.Report(new MediaConverterProgress(
                MediaConverterEventKind.FileCompleted,
                $"        Output: {finalOutputPath}{Environment.NewLine}        File runtime: {FormatDuration(fileStopwatch.Elapsed)}{Environment.NewLine}        Forced subtitle positions: {FormatList(forcedSubtitlePositions)}{Environment.NewLine}        Cleared track names: {FormatList(namedTrackPositions.Where(position => !forcedNamedTrackPositions.Contains(position)).ToList())}",
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
                $"        -> {stage} ({filePercent:0}%)",
                inputPath,
                fileIndex,
                totalFiles,
                startPercent,
                totalStopwatch.Elapsed,
                fileStopwatch.Elapsed,
                stage));
        }
    }

    private static string GetPreferredOutputPath(string inputPath, MediaConverterOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            return string.Equals(Path.GetExtension(inputPath), ".mkv", StringComparison.OrdinalIgnoreCase)
                ? inputPath
                : Path.ChangeExtension(inputPath, ".mkv");
        }

        var outputFileName = Path.ChangeExtension(Path.GetFileName(inputPath), ".mkv");
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

    private static List<int> GetForcedSubtitlePositions(MkvInfo info)
    {
        var positions = new List<int>();
        var subtitlePosition = 0;

        foreach (var track in info.Tracks)
        {
            if (!ShouldIncludeSubtitleTrack(track))
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

    private static bool ShouldIncludeSubtitleTrack(MkvTrack track)
    {
        if (!string.Equals(track.Type, "subtitles", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var language = track.Properties.Language;
        return string.IsNullOrWhiteSpace(language) ||
            SelectedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase);
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

    private static string BuildMkvMergeArguments(string inputPath, string outputPath)
    {
        return string.Join(
            ' ',
            [
                "--output",
                $"\"{outputPath}\"",
                "--audio-tracks",
                "eng,und",
                "--subtitle-tracks",
                "eng,und",
                "--default-language",
                "eng",
                "--title",
                "\"\"",
                "--no-global-tags",
                "--no-track-tags",
                "--no-attachments",
                "--no-chapters",
                $"\"{inputPath}\"",
            ]);
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

    private static void ValidateOptions(MediaConverterOptions options)
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

        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            Directory.CreateDirectory(options.OutputPath);
        }
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
    [JsonPropertyName("tracks")]
    public List<MkvTrack> Tracks { get; init; } = [];
}

internal sealed class MkvTrack
{
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
