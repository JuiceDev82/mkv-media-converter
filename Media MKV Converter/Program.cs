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
        var skipFragment = @"\Processing\";
        string? outputPath = null;
        var dryRun = false;
        var traverseSubfolders = true;
        var maintainFolderStructure = false;
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
            SkipFragment = skipFragment,
            OutputPath = string.IsNullOrWhiteSpace(outputPath) ? null : Path.GetFullPath(outputPath),
            OverwriteExisting = true,
            TraverseSubfolders = traverseSubfolders,
            MaintainFolderStructure = maintainFolderStructure,
            DryRun = dryRun,
        };
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
        Console.WriteLine("  --output <path>        Optional output folder. Converted MKVs go directly there");
        Console.WriteLine("  --skip <fragment>      Path fragment to skip. Default: \\Processing\\");
        Console.WriteLine("  --no-subfolders        Process only the input folder itself");
        Console.WriteLine("  --maintain-structure   Recreate input subfolders under --output");
        Console.WriteLine("  --dry-run              Show what would happen without changing files");
        Console.WriteLine("  --help                 Show this help");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine(@"  MediaMkvConverter ""D:\Videos\Movies"" --output ""E:\Converted"" --skip ""\Processing\""");
    }
}
