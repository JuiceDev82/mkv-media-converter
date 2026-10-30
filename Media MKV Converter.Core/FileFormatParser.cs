namespace Media_MKV_Converter.Core;

/// <summary>
/// Parses the comma-separated file-formats text a user types ("mkv, mp4, avi") into the normalized,
/// dot-prefixed extension list <see cref="MediaConverterEngine.EnumerateInputFiles"/> actually
/// filters on. A leading dot is accepted but not required, so "mkv" and ".mkv" parse the same.
/// </summary>
public static class FileFormatParser
{
    /// <summary>
    /// Returns an empty list for blank input — <see cref="MediaConverterOptions.FileFormats"/>
    /// treats that as "no filtering," not "keep the default three," since the GUI and console both
    /// pre-fill the default themselves rather than ever actually submitting a blank value by accident.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => "." + token.TrimStart('.').ToLowerInvariant())
            .Where(extension => extension.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
