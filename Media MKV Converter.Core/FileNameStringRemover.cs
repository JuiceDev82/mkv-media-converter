namespace Media_MKV_Converter.Core;

/// <summary>
/// Deletes literal substrings from a file name and tidies whatever separator debris the deletion
/// leaves behind. "Show.1080p.x265-ELiTE" with "1080p, x265" removed becomes "Show.-ELiTE" before
/// cleanup and "Show-ELiTE" after it. Applied after <see cref="FileNameTrimmer"/> in the output
/// pipeline, so it can clean up text that sits before a trim-after cut point as well as after it.
/// </summary>
public static class FileNameStringRemover
{
    private static readonly char[] Separators = ['.', '-', '_', ' '];

    /// <summary>
    /// Removes every occurrence of each pattern (case-insensitive), then collapses any resulting
    /// run of separator characters into a single one and trims them from both ends. Falls back to
    /// the original name if removal would leave nothing (or only separators) behind, the same way
    /// <see cref="FileNameTrimmer.Trim"/> refuses to empty a name. Leaves the name completely
    /// untouched — no cleanup pass either — when no pattern actually matched anything: a file's
    /// existing " - " or ".." is not this method's business to tidy unless a removal disturbed it.
    /// </summary>
    public static string Remove(string fileNameWithoutExtension, IEnumerable<string>? patterns)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExtension) || patterns is null)
        {
            return fileNameWithoutExtension;
        }

        var working = fileNameWithoutExtension;
        var matchedAnything = false;

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            var next = working.Replace(pattern.Trim(), string.Empty, StringComparison.OrdinalIgnoreCase);
            if (next != working)
            {
                matchedAnything = true;
                working = next;
            }
        }

        if (!matchedAnything)
        {
            return fileNameWithoutExtension;
        }

        var cleaned = CollapseSeparators(working);
        return string.IsNullOrWhiteSpace(cleaned) ? fileNameWithoutExtension : cleaned;
    }

    /// <summary>Splits comma-separated input into trimmed, non-blank entries.</summary>
    public static IReadOnlyList<string> ParsePatterns(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string CollapseSeparators(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length);

        foreach (var character in name)
        {
            if (Separators.Contains(character) && builder.Length > 0 && Separators.Contains(builder[^1]))
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim(Separators);
    }
}
