using Media_MKV_Converter.Core;

var exitCode = await ProgramEntry.RunAsync(args);
return exitCode;

internal static class ProgramEntry
{
    public static async Task<int> RunAsync(string[] args)
    {
        MediaConverterOptions options;

        try
        {
            options = ParseOptions(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 1;
        }

        if (options.RootPath == "__help__")
        {
            PrintUsage();
            return 0;
        }

        var progress = new Progress<MediaConverterProgress>(UpdateConsole);
        var engine = new MediaConverterEngine();

        try
        {
            var summary = await engine.RunAsync(options, progress);
            return summary.FailureCount == 0 ? 0 : 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation canceled.");
            return 1;
        }
    }

    private static MediaConverterOptions ParseOptions(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Missing root path.");
        }

        string? rootPath = null;
        string? mkvMergePath = null;
        string? mkvPropEditPath = null;
        string? mkvToolNixGuiPath = null;
        var openInMkvToolNixGui = false;
        var skipFragment = @"\Processing\";
        string? outputPath = null;
        var languages = new List<string>();
        string? primaryLanguage = null;
        var excludeCommentaryTracks = true;
        var trimFromStart = TimeSpan.Zero;
        var trimFromEnd = TimeSpan.Zero;
        string? trimNameAfter = null;
        var removeFromName = new List<string>();
        var replaceInName = new List<(string Find, string ReplaceWith)>();
        var dryRun = false;
        var traverseSubfolders = true;
        var maintainFolderStructure = false;
        var maxParallelFiles = 1;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            if (arg is "--help" or "-h" or "/?")
            {
                showHelp = true;
                continue;
            }

            if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
                continue;
            }

            if (arg.Equals("--no-subfolders", StringComparison.OrdinalIgnoreCase))
            {
                traverseSubfolders = false;
                continue;
            }

            if (arg.Equals("--maintain-structure", StringComparison.OrdinalIgnoreCase))
            {
                maintainFolderStructure = true;
                continue;
            }

            if (arg.Equals("--mkvmerge", StringComparison.OrdinalIgnoreCase))
            {
                mkvMergePath = RequireValue(args, ref index, arg);
                continue;
            }

            if (arg.Equals("--mkvpropedit", StringComparison.OrdinalIgnoreCase))
            {
                mkvPropEditPath = RequireValue(args, ref index, arg);
                continue;
            }

            if (arg.Equals("--mkvtoolnix-gui", StringComparison.OrdinalIgnoreCase))
            {
                mkvToolNixGuiPath = RequireValue(args, ref index, arg);
                continue;
            }

            if (arg.Equals("--open-in-mkvtoolnix-gui", StringComparison.OrdinalIgnoreCase))
            {
                openInMkvToolNixGui = true;
                continue;
            }

            if (arg.Equals("--languages", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--language", StringComparison.OrdinalIgnoreCase))
            {
                languages.AddRange(SplitLanguages(RequireValue(args, ref index, arg)));
                continue;
            }

            if (arg.Equals("--primary-language", StringComparison.OrdinalIgnoreCase))
            {
                primaryLanguage = RequireValue(args, ref index, arg);
                continue;
            }

            if (arg.Equals("--include-commentary", StringComparison.OrdinalIgnoreCase))
            {
                excludeCommentaryTracks = false;
                continue;
            }

            if (arg.Equals("--parallel", StringComparison.OrdinalIgnoreCase))
            {
                var rawParallel = RequireValue(args, ref index, arg);
                if (!int.TryParse(rawParallel, out maxParallelFiles) || maxParallelFiles < 1)
                {
                    throw new ArgumentException($"Invalid value for --parallel: '{rawParallel}'. Use a whole number, 1 or more.");
                }

                continue;
            }

            if (arg.Equals("--trim-start", StringComparison.OrdinalIgnoreCase))
            {
                var rawTrim = RequireValue(args, ref index, arg);
                if (!DurationParser.TryParse(rawTrim, out trimFromStart))
                {
                    throw new ArgumentException(
                        $"Invalid duration for --trim-start: '{rawTrim}'. Try 10m, 90s, 1h30m or 00:10:00.");
                }

                continue;
            }

            if (arg.Equals("--trim-end", StringComparison.OrdinalIgnoreCase))
            {
                var rawTrim = RequireValue(args, ref index, arg);
                if (!DurationParser.TryParse(rawTrim, out trimFromEnd))
                {
                    throw new ArgumentException(
                        $"Invalid duration for --trim-end: '{rawTrim}'. Try 10m, 90s, 1h30m or 00:10:00.");
                }

                continue;
            }

            if (arg.Equals("--trim-name", StringComparison.OrdinalIgnoreCase))
            {
                trimNameAfter = RequireValue(args, ref index, arg);
                if (!FileNameTrimmer.IsValidPattern(trimNameAfter))
                {
                    throw new ArgumentException(
                        $"Invalid pattern for --trim-name: '{trimNameAfter}'. It needs at least one ordinary character.");
                }

                continue;
            }

            if (arg.Equals("--remove-from-name", StringComparison.OrdinalIgnoreCase))
            {
                removeFromName.AddRange(FileNameStringRemover.ParsePatterns(RequireValue(args, ref index, arg)));
                continue;
            }

            if (arg.Equals("--replace-in-name", StringComparison.OrdinalIgnoreCase))
            {
                replaceInName.AddRange(FileNameReplacer.ParsePairs(RequireValue(args, ref index, arg)));
                continue;
            }

            if (arg.Equals("--skip", StringComparison.OrdinalIgnoreCase))
            {
                skipFragment = RequireValue(args, ref index, arg);
                continue;
            }

            if (arg.Equals("--output", StringComparison.OrdinalIgnoreCase))
            {
                outputPath = RequireValue(args, ref index, arg);
                continue;
            }

            if (arg.StartsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Unknown option: {arg}");
            }

            if (rootPath is not null)
            {
                throw new ArgumentException("Only one root path can be provided.");
            }

            rootPath = arg;
        }

        if (showHelp)
        {
            return new MediaConverterOptions { RootPath = "__help__" };
        }

        if (rootPath is null)
        {
            throw new ArgumentException("Missing root path.");
        }

        return new MediaConverterOptions
        {
            RootPath = Path.GetFullPath(rootPath),
            MkvMergePath = mkvMergePath is null ? new MediaConverterOptions().MkvMergePath : Path.GetFullPath(mkvMergePath),
            MkvPropEditPath = mkvPropEditPath is null ? new MediaConverterOptions().MkvPropEditPath : Path.GetFullPath(mkvPropEditPath),
            MkvToolNixGuiPath = mkvToolNixGuiPath is null ? new MediaConverterOptions().MkvToolNixGuiPath : Path.GetFullPath(mkvToolNixGuiPath),
            OpenInMkvToolNixGui = openInMkvToolNixGui,
            SkipFragment = skipFragment,
            Languages = languages.Count == 0 ? [LanguageCatalog.DefaultCode] : languages,
            PrimaryLanguage = primaryLanguage,
            ExcludeCommentaryTracks = excludeCommentaryTracks,
            MaxParallelFiles = maxParallelFiles,
            TrimFromStart = trimFromStart,
            TrimFromEnd = trimFromEnd,
            TrimNameAfter = trimNameAfter,
            RemoveFromName = removeFromName,
            ReplaceInName = replaceInName,
            OutputPath = string.IsNullOrWhiteSpace(outputPath) ? null : Path.GetFullPath(outputPath),
            OverwriteExisting = true,
            TraverseSubfolders = traverseSubfolders,
            MaintainFolderStructure = maintainFolderStructure,
            DryRun = dryRun,
        };
    }

    private static IEnumerable<string> SplitLanguages(string value)
    {
        return value
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {option}");
        }

        index++;
        return args[index];
    }

    private static void UpdateConsole(MediaConverterProgress update)
    {
        Console.WriteLine(update.Message);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Media MKV Converter");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  MediaMkvConverter <rootPath> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --mkvmerge <path>      Path to mkvmerge.exe");
        Console.WriteLine("  --mkvpropedit <path>   Path to mkvpropedit.exe");
        Console.WriteLine("  --mkvtoolnix-gui <p>   Path to mkvtoolnix-gui.exe, used with --open-in-mkvtoolnix-gui");
        Console.WriteLine("  --open-in-mkvtoolnix-gui  Open each file's original and finished output in MKVToolNix's GUI");
        Console.WriteLine("  --output <path>        Optional output folder. Converted MKVs go directly there");
        Console.WriteLine("  --languages <codes>    Audio/subtitle languages to keep. Default: eng");
        Console.WriteLine("  --primary-language <c> Audio language to flag as default. Default: eng, if kept");
        Console.WriteLine("  --include-commentary   Keep commentary-named audio tracks. Default: excluded");
        Console.WriteLine("  --parallel <n>         Process this many files at once. Default: 1 (sequential)");
        Console.WriteLine("  --trim-start <dur>     Cut this much off the start, e.g. intros/recaps. Default: none");
        Console.WriteLine("  --trim-end <duration>  Cut this much off the end, e.g. credits. Default: none");
        Console.WriteLine("  --trim-name <pattern>  Cut the file name short after this pattern. Default: none");
        Console.WriteLine("  --remove-from-name <s> Comma-separated text to delete from the file name. Default: none");
        Console.WriteLine("  --replace-in-name <s>  Comma-separated find=replace pairs for the file name. Default: none");
        Console.WriteLine("  --skip <fragment>      Path fragment to skip. Default: \\Processing\\");
        Console.WriteLine("  --no-subfolders        Process only the input folder itself");
        Console.WriteLine("  --maintain-structure   Recreate input subfolders under --output");
        Console.WriteLine("  --dry-run              Show what would happen without changing files");
        Console.WriteLine("  --help                 Show this help");
        Console.WriteLine();
        Console.WriteLine("Languages:");
        Console.WriteLine("  Comma-separated ISO 639 codes, e.g. --languages eng,spa,jpn");
        Console.WriteLine("  Undetermined (und) is always kept, so untagged tracks are never dropped.");
        Console.WriteLine();

        foreach (var chunk in LanguageCatalog.All.Chunk(4))
        {
            Console.WriteLine("  " + string.Join("  ", chunk.Select(option => $"{option.Code} {option.DisplayName}".PadRight(16))).TrimEnd());
        }

        Console.WriteLine();
        Console.WriteLine("  Any other ISO 639 code works too; the list above is just the common ones.");
        Console.WriteLine();
        Console.WriteLine("  --primary-language must also be one of --languages. Defaults to eng when eng is kept");
        Console.WriteLine("  and nothing else is specified; otherwise there is no implicit default. Applied even");
        Console.WriteLine("  when only one audio track survives the remux: mkvmerge can leave that lone track");
        Console.WriteLine("  non-default if a different, now-dropped track was the source file's own default.");
        Console.WriteLine();
        Console.WriteLine("  Commentary tracks share their language with the main track, so language selection");
        Console.WriteLine("  alone can't tell them apart. Any audio track named containing \"commentary\" is");
        Console.WriteLine("  dropped by default, regardless of language; --include-commentary turns that off.");
        Console.WriteLine();
        Console.WriteLine("  --parallel processes several files at once instead of one at a time. Log lines from");
        Console.WriteLine("  different files interleave when this is above 1, but each line still names its file.");
        Console.WriteLine("  Match this to your disk and CPU — a spinning HDD can get slower with more, not faster.");
        Console.WriteLine();
        Console.WriteLine("Trimming:");
        Console.WriteLine("  --trim-start/--trim-end take 10m, 90s, 1h30m, 00:10:00 or 10:00. A bare number means minutes.");
        Console.WriteLine("  mkvmerge cuts each bound on a keyframe at or after the requested point: the end runs");
        Console.WriteLine("  slightly long, but for the same reason --trim-start cuts a little more than asked.");
        Console.WriteLine("  A file too short for the requested trim is left alone and reported as a failure.");
        Console.WriteLine();
        Console.WriteLine("Name trimming:");
        Console.WriteLine("  --trim-name drops everything after the first match, keeping the .mkv extension.");
        Console.WriteLine("  Each '*' stands for exactly one character, so S01E** matches S01E07 but not S01E070.");
        Console.WriteLine("  Matching ignores case, and a name that does not match is left alone.");
        Console.WriteLine();
        Console.WriteLine("    --trim-name \"S01E**\"");
        Console.WriteLine("    Lanterns.2026.S01E07.1080p.x265-ELiTE.mkv  ->  Lanterns.2026.S01E07.mkv");
        Console.WriteLine();
        Console.WriteLine("  --remove-from-name deletes each comma-separated string wherever it occurs (case");
        Console.WriteLine("  insensitive) and tidies up the leftover dots/dashes. Runs after --trim-name.");
        Console.WriteLine();
        Console.WriteLine("    --trim-name \"S01E**\" --remove-from-name \"REPACK,PROPER\"");
        Console.WriteLine("    Show.PROPER.S01E07.1080p.x265-ELiTE.mkv  ->  Show.S01E07.mkv");
        Console.WriteLine();
        Console.WriteLine("  --replace-in-name takes \"find=replace\" pairs, comma-separated, applied in order after");
        Console.WriteLine("  --trim-name and --remove-from-name. The find side is trimmed; the replace side is kept");
        Console.WriteLine("  exactly as typed, so a trailing space survives — \".= \" (period, equals, one space)");
        Console.WriteLine("  replaces every period with a space. No cleanup runs afterward, unlike --remove-from-name:");
        Console.WriteLine("  a replace is a precise substitution, not something that leaves debris to tidy up.");
        Console.WriteLine();
        Console.WriteLine("    --replace-in-name \".= ,_= \"");
        Console.WriteLine("    Show.Name_2026.S01E07.mkv  ->  Show Name 2026 S01E07.mkv");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine(@"  MediaMkvConverter ""D:\Videos\Movies"" --output ""E:\Converted"" --languages eng,fre");
        Console.WriteLine(@"  MediaMkvConverter ""D:\Videos\Show"" --trim-end 10m");
    }
}
