using System.Text.Json;

namespace Media_MKV_Converter.Gui;

/// <summary>
/// The MKVToolNix install folder last used on this machine — mkvmerge.exe, mkvpropedit.exe, and
/// mkvtoolnix-gui.exe all live together in one folder, so the GUI only asks for that folder once
/// rather than browsing to each .exe separately. Deliberately separate from <see cref="ConversionPreset"/>
/// — this describes the machine, not the content, the same reason tool paths are excluded from
/// presets — and it's a single record, not a named list: there's nothing to pick between, just
/// whatever was used last.
/// </summary>
public sealed class ToolPathSettings
{
    public string? MkvToolNixFolder { get; set; }
}

/// <summary>
/// Loads and saves <see cref="ToolPathSettings"/> as JSON at
/// <c>%AppData%\MediaMkvConverter\toolpaths.json</c>, alongside <see cref="PresetStore"/>'s file but
/// never merged into it.
/// </summary>
public static class ToolPathSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MediaMkvConverter",
        "toolpaths.json");

    /// <summary>Returns a blank settings object rather than throwing when the file is missing or
    /// unreadable — the GUI falls back to empty text boxes, never a guessed path.</summary>
    public static ToolPathSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new ToolPathSettings();
            }

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<ToolPathSettings>(json, JsonOptions) ?? new ToolPathSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new ToolPathSettings();
        }
    }

    /// <summary>Best-effort save. Unlike <see cref="PresetStore.Save"/>, this fires on routine field
    /// edits rather than an explicit "Save" click, so a transient I/O error is swallowed instead of
    /// surfacing an error dialog for something the user didn't directly ask to happen.</summary>
    public static void Save(ToolPathSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
