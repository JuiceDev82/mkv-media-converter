namespace Media_MKV_Converter.Core;

/// <summary>
/// Truncates a file name just after a pattern match, to drop release tags from the tail.
/// "Lanterns.2026.S01E07.1080p.x265-ELiTE" with the pattern "S01E**" becomes "Lanterns.2026.S01E07".
/// </summary>
public static class FileNameTrimmer
{
    private const char Wildcard = '*';
    private static readonly char[] TrailingSeparators = ['.', '-', '_', ' '];

    /// <summary>
    /// Returns the name truncated after the first match, or the name unchanged when the pattern is
    /// blank, does not match, or would leave nothing behind. Matching ignores case.
    /// </summary>
    public static string Trim(string fileNameWithoutExtension, string? pattern)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExtension) || string.IsNullOrWhiteSpace(pattern))
        {
            return fileNameWithoutExtension;
        }

        // Each wildcard stands for exactly one character, so a match is always the pattern's length.
        var trimmedPattern = pattern.Trim();
        if (trimmedPattern.Length > fileNameWithoutExtension.Length)
        {
            return fileNameWithoutExtension;
        }

        for (var start = 0; start + trimmedPattern.Length <= fileNameWithoutExtension.Length; start++)
        {
            if (!MatchesAt(fileNameWithoutExtension, trimmedPattern, start))
            {
                continue;
            }

            var kept = fileNameWithoutExtension[..(start + trimmedPattern.Length)]
                .TrimEnd(TrailingSeparators);

            return kept.Length == 0 ? fileNameWithoutExtension : kept;
        }

        return fileNameWithoutExtension;
    }

    /// <summary>
    /// A usable pattern has at least one literal character. An all-wildcard pattern would match at
    /// the very start of every name and truncate them all to nothing.
    /// </summary>
    public static bool IsValidPattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        return pattern.Trim().Any(character => character != Wildcard);
    }

    private static bool MatchesAt(string name, string pattern, int start)
    {
        for (var offset = 0; offset < pattern.Length; offset++)
        {
            if (pattern[offset] == Wildcard)
            {
                continue;
            }

            if (char.ToLowerInvariant(pattern[offset]) != char.ToLowerInvariant(name[start + offset]))
            {
                return false;
            }
        }

        return true;
    }
}
