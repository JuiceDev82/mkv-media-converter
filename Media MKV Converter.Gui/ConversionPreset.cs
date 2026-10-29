using System.Text.Json;
using System.Text.Json.Serialization;
using Media_MKV_Converter.Core;

namespace Media_MKV_Converter.Gui;

/// <summary>
/// A saved bundle of the "what kind of show is this" settings — languages, name cleanup, audio
/// track handling — so a library with mixed content (anime vs. Western TV, say) doesn't need its
/// settings re-entered by hand every time. Deliberately excludes folder-mechanics settings (skip
/// fragment, maintain-structure, overwrite, dry-run, tool paths, input/output folders): those stay
/// with the current session rather than becoming part of a reusable profile. Text fields are kept
/// as the raw strings the corresponding text box held, not pre-parsed, so a preset round-trips
/// through the same parsing/validation the field would get from direct typing.
/// </summary>
public sealed class ConversionPreset
{
    public string Name { get; set; } = string.Empty;

    public List<string> Languages { get; set; } = [];

    public string? PrimaryLanguage { get; set; }

    public bool ExcludeCommentaryTracks { get; set; } = true;

    public string? TrimFromStart { get; set; }

    public string? TrimFromEnd { get; set; }

    public string? TrimNameAfter { get; set; }

    public string? RemoveFromName { get; set; }

    /// <summary>Saved and restored with no trimming, unlike every other string field here — a
    /// trailing space can be the replacement value itself ("replace periods with spaces"), and
    /// trimming it on save would silently break that exact round-trip. See <see cref="FileNameReplacer"/>.</summary>
    public string? ReplaceInName { get; set; }

    /// <summary>Lets a plain <c>ComboBox.Items.Add(preset)</c> show the name without a DisplayMember.</summary>
    public override string ToString() => Name;
}

/// <summary>
/// Loads and saves <see cref="ConversionPreset"/>s as JSON at
/// <c>%AppData%\MediaMkvConverter\presets.json</c>. Every method is defensive about the file being
/// missing, empty, or corrupt — presets are a convenience layered on top of the app, never something
/// a run should fail over.
/// </summary>
public static class PresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MediaMkvConverter",
        "presets.json");

    /// <summary>Returns an empty list rather than throwing when the file is missing or unreadable.</summary>
    public static List<ConversionPreset> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var json = File.ReadAllText(FilePath);
            var presets = JsonSerializer.Deserialize<List<ConversionPreset>>(json, JsonOptions);
            return presets ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <summary>Overwrites the whole preset file. Throws on failure — unlike <see cref="Load"/>, a
    /// save the user explicitly asked for should surface an error rather than silently no-op.</summary>
    public static void Save(IReadOnlyList<ConversionPreset> presets)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(presets, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
