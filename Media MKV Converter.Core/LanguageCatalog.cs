namespace Media_MKV_Converter.Core;

/// <summary>
/// A language the converter can keep. <paramref name="Code"/> is the ISO 639-2/B code mkvmerge
/// reports; <paramref name="AlternateCode"/> carries the 639-2/T spelling for the handful of
/// languages that have two (fre/fra, ger/deu, chi/zho), so either tag in a source file matches.
/// </summary>
public sealed record LanguageOption(string Code, string DisplayName, string? AlternateCode = null)
{
    public bool Matches(string language) =>
        string.Equals(Code, language, StringComparison.OrdinalIgnoreCase) ||
        (AlternateCode is not null && string.Equals(AlternateCode, language, StringComparison.OrdinalIgnoreCase));
}

public static class LanguageCatalog
{
    public const string DefaultCode = "eng";

    /// <summary>
    /// Kept on every run regardless of what was selected. mkvmerge tags a track with no language
    /// as undetermined, and dropping those can leave a file with no audio at all.
    /// </summary>
    public const string Undetermined = "und";

    /// <summary>English plus the ten languages most common in film and TV releases.</summary>
    public static IReadOnlyList<LanguageOption> All { get; } =
    [
        new(DefaultCode, "English"),
        new("spa", "Spanish"),
        new("fre", "French", "fra"),
        new("ger", "German", "deu"),
        new("ita", "Italian"),
        new("por", "Portuguese"),
        new("rus", "Russian"),
        new("jpn", "Japanese"),
        new("kor", "Korean"),
        new("chi", "Chinese", "zho"),
        new("hin", "Hindi"),
    ];

    public static LanguageOption? Find(string code) =>
        All.FirstOrDefault(option => option.Matches(code));

    /// <summary>
    /// Trims, lower-cases and de-duplicates the requested codes, folds alternate spellings onto the
    /// code mkvmerge reports, and appends <see cref="Undetermined"/>. An empty request means English.
    /// </summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? codes)
    {
        var normalized = new List<string>();

        foreach (var code in codes ?? [])
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var trimmed = code.Trim().ToLowerInvariant();
            var canonical = Find(trimmed)?.Code ?? trimmed;
            if (!normalized.Contains(canonical))
            {
                normalized.Add(canonical);
            }
        }

        if (normalized.Count == 0)
        {
            normalized.Add(DefaultCode);
        }

        if (!normalized.Contains(Undetermined))
        {
            normalized.Add(Undetermined);
        }

        return normalized;
    }

    /// <summary>True when a track's language tag is one this run keeps.</summary>
    public static bool IsSelected(IReadOnlyList<string> selected, string language)
    {
        foreach (var code in selected)
        {
            if (string.Equals(code, language, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (Find(code) is { } option && option.Matches(language))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The language mkvmerge stamps onto tracks that arrive with no tag of their own.</summary>
    public static string GetDefaultLanguage(IReadOnlyList<string> selected) =>
        selected.FirstOrDefault(code => !string.Equals(code, Undetermined, StringComparison.OrdinalIgnoreCase))
        ?? DefaultCode;

    public static bool IsValidCode(string code) =>
        code.Length is 2 or 3 && code.All(char.IsAsciiLetter);
}
