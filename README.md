# Media MKV Converter

Batch MKV remuxer that drives the [MKVToolNix](https://mkvtoolnix.download/) command-line tools. It walks a folder of media files, strips them down to the audio and subtitle languages you want to keep (English by default), removes cruft (titles, tags, attachments, chapters), and normalises subtitle track flags and names — so a library ends up with consistent, clean MKVs.

Remuxing only: no re-encoding, so video and audio are copied bit-for-bit and runs are I/O-bound rather than CPU-bound.

## What it does to each file

1. **Inspect** — `mkvmerge -J` the source and parse the JSON track layout.
2. **Remux** — write a temporary sibling file keeping only the selected languages (minus any commentary tracks, by default), dropping the title, global and per-track tags, attachments, and chapters, and cutting the tail if a trim is set.
3. **Replace** — move the temp file into place (in-place) or into a destination folder (`--output`), under a trimmed name if a name pattern is set.
4. **Retag** — re-inspect the result with `mkvpropedit`: set forced flags on subtitle tracks that look forced, clear every non-forced track name, name the forced ones `Forced`, and flag the primary language's audio track as default if one was chosen.

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

Two rules apply on every language selection:

- **Undetermined (`und`) is always kept.** mkvmerge tags a track carrying no language as undetermined, and dropping those could leave a file with no audio.
- **A file is left untouched if no audio track matches your selection.** You get an error naming the languages that file actually has, rather than a silently muted result — which matters because in-place mode deletes the original.

### Primary audio language

If you keep more than one audio track, `--primary-language <code>` (console) or **Settings → Primary Language** (GUI) picks which one auto-selects on playback, overriding mkvmerge's own default (whichever kept track happened to come first in the source). It must also be one of your selected languages. On a file that only ends up keeping one audio track there's nothing to disambiguate, so it does nothing there.

### Commentary tracks

Commentary tracks are excluded by default — `--include-commentary` (console) or unchecking **Settings → Exclude Commentary Tracks** (GUI) turns that off. Language selection alone can't filter these out, since a commentary track usually carries the *same* language tag as the main one; this instead looks for `commentary` in the track's own name. If a language you selected turns out to have *only* a commentary track and no plain one, the file is left untouched with an error explaining why, the same safety net as having no matching language at all.

## Trimming the end

To drop trailing credits, give `--trim-end` (console) or fill in **Settings → Trim From End** (GUI) with how much to cut:

```powershell
MediaMkvConverter "D:\Videos\Show" --trim-end 10m
```

Accepted forms: `10m`, `90s`, `1h30m`, `30 sec`, `00:10:00`, `10:00` (mm:ss), or a bare number, which means minutes. The duration is subtracted from each file's own length, so a folder of differently-sized episodes each lose the same tail.

Two things to know before relying on it:

- **The cut lands on a keyframe, so you keep slightly more than you asked for.** Remuxing copies compressed video untouched, and a cut is only possible where a keyframe starts. mkvmerge moves the point forward to the next one, which typically leaves a few extra seconds. It errs toward keeping content rather than cutting into it. Frame-exact trimming would require re-encoding, which this tool deliberately does not do.
- **A file shorter than the trim is left alone** and reported as a failure naming its actual length.

The log reports the real result — `Trimmed: 45:00 -> 35:04` — rather than the requested figure, so you can see where the cut actually landed. Run with `--dry-run` first to preview.

## Trimming file names

To strip release tags off the end of names, give `--trim-name` (console) or fill in **Settings → Trim Name After** (GUI) with a pattern. Everything after the first match is dropped and the `.mkv` extension is kept:

```powershell
MediaMkvConverter "D:\Videos\Show" --trim-name "S01E**"
```

```
Lanterns.2026.S01E07.1080p.x265-ELiTE.mkv   ->  Lanterns.2026.S01E07.mkv
Lanterns.2026.S01E08.2160p.WEB-DL-NTb.mkv   ->  Lanterns.2026.S01E08.mkv
Movie.2024.1080p.BluRay-GRP.mkv             ->  unchanged, no match
```

**`*` stands for exactly one character**, not "any number of them" as in a shell glob. `S01E**` matches `S01E` plus two more characters, so it fits `S01E07` but not a three-digit `S01E070` — that would need `S01E***`. Use `S0*E**` to cover any season. Matching ignores case, and a name with no match is left alone, so it is safe to point at a mixed folder.

Two details worth knowing:

- **Leftover separators are tidied.** Cutting mid-name never leaves a trailing `.`, `-`, `_` or space — Windows rejects names ending in a dot anyway.
- **Files that would collide are refused.** Two releases of one episode both reduce to `Show.S01E07.mkv`; the first is converted and the second is left untouched and reported as a failure, rather than one silently overwriting the other. That matters most in in-place mode, where the loser's source would also be deleted.

`--dry-run` prints every `Renamed: old -> new` without touching anything, which is the quickest way to check a pattern.

### Removing text from anywhere in the name

`--trim-name` only cuts a suffix. To delete specific text wherever it appears — including in the middle of the part you're keeping — give `--remove-from-name` (console) or **Settings → Remove From Name** (GUI) a comma-separated list. Each entry is deleted everywhere it occurs, case-insensitively, and runs *after* `--trim-name`:

```powershell
MediaMkvConverter "D:\Videos\Show" --trim-name "S01E**" --remove-from-name "PROPER,REPACK"
```

```
Show.PROPER.S01E07.1080p.x265-ELiTE.mkv   ->  Show.S01E07.mkv
Show Title - S01E08.mkv                   ->  unchanged, nothing matched
```

Whatever dots, dashes or spaces the removal leaves stranded next to each other are collapsed to one, and trimmed from both ends — but only on a name a removal actually touched. A file where nothing matched is left **byte-for-byte** untouched, so an existing `" - "` or similar in your naming style is never disturbed by files that had no reason to change. As with trim-name, a removal that would empty the name is refused in favor of the original, and the same collision guard applies — two names that reduce to the same result leave the second file's source alone and reported as a failure.

> **In-place mode deletes the original before moving the remuxed file into place.** A crash in that window loses the source file. Use `--output` (or `--dry-run` first) if that matters to you.

## Presets (GUI)

The **Preset** dropdown at the top of Settings saves and restores the "what kind of show is this" fields in one step: languages, primary language, commentary exclusion, and all three name/trim settings. **Save As...** names and stores the current values (`%AppData%\MediaMkvConverter\presets.json`); picking a preset from the list applies it immediately; **Delete** removes the selected one. Deliberately left out: folder paths, the skip fragment, tool paths, and the overwrite/dry-run/maintain-structure/parallel-files toggles — those describe how *this run* behaves, not what kind of content it is, so they stay with the current session rather than being saved into a reusable profile.

## Selecting files to process (GUI)

Every entry in **Files To Process** has its own checkbox — uncheck one to leave it out of the run without moving it out of the folder or touching the skip fragment. A file already excluded by the skip fragment shows its checkbox locked unchecked, since that rule is meant to be unconditional; toggling it there does nothing. Unchecked files survive a live refresh (the folder watcher picking up an unrelated change elsewhere) by full path, so they don't silently get reselected out from under you.

## Parallel processing

By default files are processed one at a time. `--parallel <n>` (console) or the **Parallel Files** spinner (GUI) raises that; try 2–4 and see what your disk can take — remuxing is I/O-bound, so a spinning HDD may get *slower*, not faster, with more than one file writing to it at once. Correctness doesn't change: the same collision guard, audio and duration safety checks apply regardless of how many files run at once. Two things do change above 1:

- **Log lines interleave.** Every stage line names its own file (`[Show.S01E07.mkv] -> remux (25%)`), so the log stays readable, just no longer strictly one file at a time top to bottom.
- **A same-run naming collision is decided by timing, not alphabetical order.** Sequentially, the first file alphabetically always wins a collision. In parallel, whichever file's inspect stage happens to finish first wins — still exactly one winner, the rest still refused and reported as failures, just no longer predictable in advance.

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
  --primary-language <c> Audio language to flag as default when 2+ are kept. Default: none
  --include-commentary   Keep commentary-named audio tracks. Default: excluded
  --trim-end <duration>  Cut this much off the end, e.g. credits. Default: none
  --trim-name <pattern>  Cut the file name short after this pattern. Default: none
  --remove-from-name <s> Comma-separated text to delete from the file name. Default: none
  --parallel <n>         Process this many files at once. Default: 1 (sequential)
  --skip <fragment>      Path fragment to skip. Default: \Processing\
  --no-subfolders        Process only the input folder itself
  --maintain-structure   Recreate input subfolders under --output
  --dry-run              Show what would happen without changing files
  --help                 Show this help
```

Exit code is `0` when every file succeeded, `1` otherwise.

Any file whose path contains the skip fragment (`\Processing\` by default) is passed over — handy for pointing the converter at a library that has an active download or staging folder inside it.

## GUI

`Media MKV Converter.Gui` is a WinForms shell over the same engine: input and output folder pickers, a preset picker, the tool paths, the skip fragment, the language checkboxes, the primary-language, trim-from-end, trim-name and remove-from-name fields, a Parallel Files spinner, and toggles for subfolder traversal, folder-structure mirroring, overwrite, dry-run, and commentary exclusion. Progress lines are colour-coded by event kind.

`AppIcon.ico` (a generated play-badge icon) is embedded into the exe via `<ApplicationIcon>` and re-applied to the running window at startup — see `MainForm.ApplyApplicationIcon`. To use your own icon instead, replace the file and keep the name, or update `<ApplicationIcon>` in the `.csproj` to point at a different one.

The **Files To Process** panel watches the input folder live: add, remove, or rename a file there while the app is open (with a file manager, another program, anything) and the list updates on its own within about half a second, no need to reopen or re-browse the folder. A **Refresh** button next to it forces an immediate re-scan, which also covers the rare case where watching itself fails — some network or virtual drives don't support it, and it's silently disabled rather than treated as an error. Watching is paused for the duration of a run, since a conversion is itself creating, renaming and deleting files in that same folder.

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
