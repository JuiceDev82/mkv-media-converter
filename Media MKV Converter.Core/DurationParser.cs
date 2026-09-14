using System.Globalization;

namespace Media_MKV_Converter.Core;

/// <summary>
/// Parses the duration strings both front ends accept, so a value typed in the GUI means exactly
/// what the same value means on the command line. Understands unit form ("10m", "1h30m", "90s",
/// "30 sec"), clock form ("00:10:00", "10:00"), and a bare number, which is read as minutes.
/// </summary>
public static class DurationParser
{
    /// <summary>
    /// Returns false only for text that cannot be understood. Blank text is valid and yields
    /// <see cref="TimeSpan.Zero"/>, which every caller treats as "no trim".
    /// </summary>
    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();

        if (trimmed.Contains(':'))
        {
            return TryParseClock(trimmed, out value);
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var bareMinutes))
        {
            if (bareMinutes < 0)
            {
                return false;
            }

            value = TimeSpan.FromMinutes(bareMinutes);
            return true;
        }

        return TryParseUnits(trimmed, out value);
    }

    /// <summary>Formats a duration the way the parser accepts it back, e.g. "10m" or "1h 30m".</summary>
    public static string Format(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "0m";
        }

        var parts = new List<string>();

        if (value.Days > 0 || value.Hours > 0)
        {
            parts.Add($"{(int)value.TotalHours}h");
        }

        if (value.Minutes > 0)
        {
            parts.Add($"{value.Minutes}m");
        }

        if (value.Seconds > 0 || parts.Count == 0)
        {
            parts.Add($"{value.Seconds}s");
        }

        return string.Join(' ', parts);
    }

    private static bool TryParseClock(string text, out TimeSpan value)
    {
        value = TimeSpan.Zero;

        var segments = text.Split(':');
        if (segments.Length is not (2 or 3))
        {
            return false;
        }

        var total = TimeSpan.Zero;

        if (segments.Length == 3)
        {
            if (!int.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) || hours < 0)
            {
                return false;
            }

            total += TimeSpan.FromHours(hours);
            segments = [segments[1], segments[2]];
        }

        if (!int.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) || minutes < 0)
        {
            return false;
        }

        if (!double.TryParse(segments[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds < 0)
        {
            return false;
        }

        value = total + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        return true;
    }

    private static bool TryParseUnits(string text, out TimeSpan value)
    {
        value = TimeSpan.Zero;

        var total = TimeSpan.Zero;
        var index = 0;
        var matchedAnything = false;

        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
                continue;
            }

            var numberStart = index;
            while (index < text.Length && (char.IsAsciiDigit(text[index]) || text[index] == '.'))
            {
                index++;
            }

            if (index == numberStart)
            {
                return false;
            }

            if (!double.TryParse(text[numberStart..index], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)
                || amount < 0)
            {
                return false;
            }

            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            var unitStart = index;
            while (index < text.Length && char.IsAsciiLetter(text[index]))
            {
                index++;
            }

            if (index == unitStart)
            {
                return false;
            }

            var unit = text[unitStart..index].ToLowerInvariant();

            // "ms" is rejected rather than silently read as minutes.
            if (unit.StartsWith("ms", StringComparison.Ordinal))
            {
                return false;
            }

            TimeSpan segment;
            if (unit.StartsWith('h'))
            {
                segment = TimeSpan.FromHours(amount);
            }
            else if (unit.StartsWith('m'))
            {
                segment = TimeSpan.FromMinutes(amount);
            }
            else if (unit.StartsWith('s'))
            {
                segment = TimeSpan.FromSeconds(amount);
            }
            else
            {
                return false;
            }

            total += segment;
            matchedAnything = true;
        }

        if (!matchedAnything)
        {
            return false;
        }

        value = total;
        return true;
    }
}
