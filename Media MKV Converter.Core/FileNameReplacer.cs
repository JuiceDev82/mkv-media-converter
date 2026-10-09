namespace Media_MKV_Converter.Core;

/// <summary>
/// Applies literal find-and-replace pairs to a file name — "replace periods with spaces" is the
/// motivating case. Unlike <see cref="FileNameStringRemover"/>, this never tidies up separators
/// afterward: a replace is a precise substitution the user asked for exactly, including when the
/// replacement is itself a separator character, so there is no deletion debris to clean up on their
/// behalf the way there is after removing text outright.
/// </summary>
public static class FileNameReplacer
{
    /// <summary>
    /// Applies every (find, replace) pair in order, case-insensitively. Falls back to the original
    /// name if the result would be empty or whitespace-only, the same safety net
    /// <see cref="FileNameTrimmer.Trim"/> and <see cref="FileNameStringRemover.Remove"/> already apply.
    /// </summary>
    public static string Replace(string fileNameWithoutExtension, IEnumerable<(string Find, string ReplaceWith)>? pairs)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExtension) || pairs is null)
        {
            return fileNameWithoutExtension;
        }

        var working = fileNameWithoutExtension;

        foreach (var (find, replaceWith) in pairs)
        {
            if (find.Length == 0)
            {
                continue;
            }

            working = working.Replace(find, replaceWith, StringComparison.OrdinalIgnoreCase);
        }

        return string.IsNullOrWhiteSpace(working) ? fileNameWithoutExtension : working;
    }

    /// <summary>
    /// Parses comma-separated <c>find=replace</c> pairs. The find side is trimmed — accidental
    /// whitespace from typing or pasting around it is almost never intentional — but the replace
    /// side is kept <em>exactly</em> as typed, leading/trailing spaces included: "replace periods
    /// with spaces" needs that trailing space to survive untouched, even though it is invisible in
    /// a text box. A segment with no <c>=</c>, or whose find side is empty once trimmed, is silently
    /// skipped rather than treated as an error — consistent with how a blank entry in
    /// <see cref="FileNameStringRemover.ParsePatterns"/> is also just ignored.
    /// </summary>
    public static IReadOnlyList<(string Find, string ReplaceWith)> ParsePairs(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var pairs = new List<(string Find, string ReplaceWith)>();

        foreach (var segment in text.Split(','))
        {
            var equalsIndex = segment.IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }

            var find = segment[..equalsIndex].Trim();
            if (find.Length == 0)
            {
                continue;
            }

            var replaceWith = segment[(equalsIndex + 1)..];
            pairs.Add((find, replaceWith));
        }

        return pairs;
    }
}
