# Media MKV Converter

Batch MKV remuxer that drives the [MKVToolNix](https://mkvtoolnix.download/) command-line tools. It walks a folder of media files, strips them down to the audio and subtitle languages you want to keep (English by default), removes cruft (titles, tags, attachments, chapters), and normalises subtitle track flags and names — so a library ends up with consistent, clean MKVs.

Remuxing only: no re-encoding, so video and audio are copied bit-for-bit and runs are I/O-bound rather than CPU-bound.

## What it does to each file

1. **Inspect** — `mkvmerge -J` the source and parse the JSON track layout.
2. **Remux** — write a temporary sibling file keeping only the selected languages, dropping the title, global and per-track tags, attachments, and chapters.
3. **Replace** — move the temp file into place (in-place) or into a destination folder (`--output`).
4. **Retag** — re-inspect the result with `mkvpropedit`: set forced flags on subtitle tracks that look forced, clear every non-forced track name, and name the forced ones `Forced`.

A subtitle track counts as forced when its name contains `forced` or `foreign`, or when its `forced_track` flag is already set.

Supported inputs: `.mkv`, `.mp4`, `.avi`. Output is always MKV.

## Languages

By default the tool keeps English only. Pass `--languages` (console) or tick boxes under **Settings → Languages** (GUI) to keep more:

```powershell
MediaMkvConverter "D:\Videos" --languages eng,fre,jpn
```

| Code | Language | Code | Language | Code | Language |
| --- | --- | --- | --- | --- | --- |
| `eng` | English | `por` | Portuguese | `kor` | Korean |
| `spa` | Spanish | `rus` | Russian | `chi` | Chinese |
| `fre` | French | `jpn` | Japanese | `hin` | Hindi |
| `ger` | German | `ita` | Italian | | |

Any other ISO 639 code works too — the list above is only what the GUI offers as checkboxes. Codes with two ISO 639-2 spellings are interchangeable (`fra`/`fre`, `deu`/`ger`, `zho`/`chi`).

Two rules apply on every run:

- **Undetermined (`und`) is always kept.** mkvmerge tags a track carrying no language as undetermined, and dropping those could leave a file with no audio.
- **A file is left untouched if no audio track matches your selection.** You get an error naming the languages that file actually has, rather than a silently muted result — which matters because in-place mode deletes the original.

> **In-place mode deletes the original before moving the remuxed file into place.** A crash in that window loses the source file. Use `--output` (or `--dry-run` first) if that matters to you.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download) or newer to build (a newer SDK builds the `net9.0` targets fine).
- MKVToolNix — `mkvmerge.exe` and `mkvpropedit.exe`. Both default to `%LOCALAPPDATA%\mkvtoolnix\` and are validated before a run starts; point at them explicitly if yours live elsewhere.
- Windows for the GUI (WinForms). The core engine and console front end target `net9.0`.

## Build

```powershell
dotnet build "Media MKV Converter.sln"      # everything
dotnet build "Media MKV Converter.Gui"      # just the GUI
```

## Console usage

```powershell
dotnet run --project "Media MKV Converter" -- "D:\Videos" --dry-run
```

```
MediaMkvConverter <rootPath> [options]

  --mkvmerge <path>      Path to mkvmerge.exe
  --mkvpropedit <path>   Path to mkvpropedit.exe
  --output <path>        Optional output folder. Converted MKVs go directly there
  --languages <codes>    Audio/subtitle languages to keep. Default: eng
  --skip <fragment>      Path fragment to skip. Default: \Processing\
  --no-subfolders        Process only the input folder itself
  --maintain-structure   Recreate input subfolders under --output
  --dry-run              Show what would happen without changing files
  --help                 Show this help
```

Exit code is `0` when every file succeeded, `1` otherwise.

Any file whose path contains the skip fragment (`\Processing\` by default) is passed over — handy for pointing the converter at a library that has an active download or staging folder inside it.

## GUI

`Media MKV Converter.Gui` is a WinForms shell over the same engine: input and output folder pickers, the tool paths, the skip fragment, the language checkboxes, and toggles for subfolder traversal, folder-structure mirroring, overwrite, and dry-run. Progress lines are colour-coded by event kind.

## Project layout

| Project | Target | Role |
| --- | --- | --- |
| `Media MKV Converter.Core` | `net9.0` | `MediaConverterEngine` — the entire product |
| `Media MKV Converter` | `net9.0` | Console front end |
| `Media MKV Converter.Gui` | `net9.0-windows` | WinForms front end |

Both front ends are thin: they build a `MediaConverterOptions`, subscribe an `IProgress<MediaConverterProgress>`, and render the events. Features belong in the engine.

## License

[MIT](LICENSE).

MKVToolNix is a separate GPL-2.0 project and is not bundled here — this tool invokes `mkvmerge` and `mkvpropedit` as external processes, so you install and license it on its own terms.
