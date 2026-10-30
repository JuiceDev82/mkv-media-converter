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

By default the tool keeps English only. Pass `--languages` (console) or tick boxes under **Settings → Tracks → Languages** (GUI) to keep more:

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

`--primary-language <code>` (console) or **Settings → Tracks → Primary Language** (GUI) picks which kept audio track auto-selects on playback. **Defaults to `eng`** whenever English is among the languages you're keeping and you haven't typed anything else; leave it blank to get that default, or type a different code to override it. It must be one of your selected languages — picking `fre` while only keeping `eng,jpn` is refused up front. If English isn't one of your kept languages and you didn't set anything, there's no implicit default.

This still applies with only one audio track surviving the remux, which matters more than it sounds: mkvmerge copies each kept track's own default-track flag from the *source* rather than recomputing it, so if the original file's default was a different track that got dropped (foreign-default source with the video, say), the lone remaining track can come out of the remux still flagged non-default even though it's the only audio there is. This setting corrects that explicitly, so the language you asked to keep is always the one that plays automatically.

### Commentary tracks

Commentary tracks are excluded by default — `--include-commentary` (console) or unchecking **Settings → Tracks → Exclude Commentary Tracks** (GUI) turns that off. Language selection alone can't filter these out, since a commentary track usually carries the *same* language tag as the main one; this instead looks for `commentary` in the track's own name. If a language you selected turns out to have *only* a commentary track and no plain one, the file is left untouched with an error explaining why, the same safety net as having no matching language at all.

## Trimming the start and end

To drop an intro/recap and/or trailing credits, give `--trim-start`/`--trim-end` (console) or fill in **Settings → Video Trimming → Trim From Start**/**Trim From End** (GUI) with how much to cut from each side. Either can be used alone or together:

```powershell
MediaMkvConverter "D:\Videos\Show" --trim-start 1m30s --trim-end 10m
```

Accepted forms: `10m`, `90s`, `1h30m`, `30 sec`, `00:10:00`, `10:00` (mm:ss), or a bare number, which means minutes. Each duration is relative to the file's own length, so a folder of differently-sized episodes all lose the same head and/or tail.

Two things to know before relying on it:

- **Both cuts land on a keyframe, but that cuts opposite ways.** Remuxing copies compressed video untouched, and a cut is only possible where a keyframe starts; mkvmerge always moves the requested point forward to the next one. For the end, that means you keep slightly *more* than you asked for. For the start, it means the opposite: the kept footage begins a little later than requested, so slightly *more* gets trimmed off the front than you asked for. Frame-exact trimming would require re-encoding, which this tool deliberately does not do.
- **A file too short for the requested trim(s) is left alone** and reported as a failure naming its actual length.

The log reports the real result — `Trimmed: 45:00 -> 35:04` — rather than the requested figures, so you can see where the cuts actually landed. Run with `--dry-run` first to preview.

## Trimming file names

To strip release tags off the end of names, give `--trim-name` (console) or fill in **Settings → File Naming → Trim Name After** (GUI) with a pattern. Everything after the first match is dropped and the `.mkv` extension is kept:

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

`--trim-name` only cuts a suffix. To delete specific text wherever it appears — including in the middle of the part you're keeping — give `--remove-from-name` (console) or **Settings → File Naming → Remove From Name** (GUI) a comma-separated list. Each entry is deleted everywhere it occurs, case-insensitively, and runs *after* `--trim-name`:

```powershell
MediaMkvConverter "D:\Videos\Show" --trim-name "S01E**" --remove-from-name "PROPER,REPACK"
```

```
Show.PROPER.S01E07.1080p.x265-ELiTE.mkv   ->  Show.S01E07.mkv
Show Title - S01E08.mkv                   ->  unchanged, nothing matched
```

Whatever dots, dashes or spaces the removal leaves stranded next to each other are collapsed to one, and trimmed from both ends — but only on a name a removal actually touched. A file where nothing matched is left **byte-for-byte** untouched, so an existing `" - "` or similar in your naming style is never disturbed by files that had no reason to change. As with trim-name, a removal that would empty the name is refused in favor of the original, and the same collision guard applies — two names that reduce to the same result leave the second file's source alone and reported as a failure.

### Replacing characters in the name

Removal only ever deletes. To swap one piece of text for another — the common case being punctuation, like turning dots into spaces for readability — give `--replace-in-name` (console) or **Settings → File Naming → Replace In Name** (GUI) comma-separated `find=replace` pairs. Each pair is applied in order, case-insensitively, *after* both `--trim-name` and `--remove-from-name`:

```powershell
MediaMkvConverter "D:\Videos\Show" --replace-in-name ".= "
```

```
Lanterns.2026.S01E07.1080p.x265-ELiTE.mkv   ->  Lanterns 2026 S01E07 1080p x265-ELiTE.mkv
```

**The text after `=` is kept exactly as typed — including a trailing space — all the way through.** The example above is literally "period, equals sign, one space": that trailing space is invisible in a text box, but it's what makes "replace periods with spaces" work, and nothing in this app ever trims it away (not the live preview, not a saved preset). The text *before* `=` is trimmed, so visual spacing around pairs or commas doesn't break matching — `.= , _= ` parses the same as `.= ,_= `.

Unlike `--remove-from-name`, nothing gets tidied up afterward. A replace is an exact substitution you asked for — including when the replacement is itself a separator — so there's no deletion debris to clean up the way there is after removing text outright. The only safety net is the same one every name setting has: a replacement that would empty the name out entirely is refused in favor of the original.

Multiple pairs run in sequence, so `.= ,_= ` turns both dots and underscores into spaces. Because the preview list (below) shows the real result live, confirming a space actually landed where you meant it to is a glance away rather than a guess.

> **In-place mode deletes the original before moving the remuxed file into place.** A crash in that window loses the source file. Use `--output` (or `--dry-run` first) if that matters to you.

## Presets (GUI)

The **Preset** dropdown at the top of Settings saves and restores the "what kind of show is this" fields in one step: file formats, languages, primary language, commentary exclusion, and all five name/trim settings (including Replace In Name — a saved preset preserves a trailing-space replacement exactly, the same as typing it fresh). **Save As...** names and stores the current values (`%AppData%\MediaMkvConverter\presets.json`); picking a preset from the list applies it immediately; **Delete** removes the selected one. Deliberately left out: folder paths, the skip fragment, tool paths, and the overwrite/dry-run/maintain-structure/parallel-files toggles — those describe how *this run* behaves, not what kind of content it is, so they stay with the current session rather than being saved into a reusable profile.

File Formats is the one field here that behaves a little differently from the rest: a preset saved before this field existed has nothing stored for it, and applying that preset shows the default `mkv,mp4,avi` rather than leaving the box empty — picking an old preset never silently switches you to "process every file."

A preset named exactly **Default** is special: it's selected and applied automatically every time the app opens, instead of the picker starting blank. The very first time you run the app (or any time "Default" doesn't exist — say you deleted it), one is created automatically from the form's own starting values and saved, so there's always a real "Default" to load and build on. From then on, saving over it (pick **Save As...**, keep the name `Default`, confirm the overwrite) is the normal way to change what loads next time — every other preset still has to be picked by hand, same as before.

## Tool paths (GUI)

The GUI asks for one thing — the **MKVToolNix Folder** — instead of separate paths for each tool, since `mkvmerge.exe`, `mkvpropedit.exe`, and `mkvtoolnix-gui.exe` all live together in a normal install (e.g. `C:\Program Files\MKVToolNix`). It starts blank on a machine that's never run it, rather than guessing a path the way the console default does — a guessed path that happens to not exist is worse than an obviously-empty field. Whatever folder you point it at is remembered automatically (on Browse, and when you click or tab out of the box after typing) at `%AppData%\MediaMkvConverter\toolpaths.json`, separate from presets — tool paths describe this machine, not a kind of content, so they're excluded from presets for the same reason the skip fragment and folder paths are (see above), but still worth remembering across restarts since it doesn't normally change.

**Verify In MKVToolNix** (right below the folder) opens each file's original, then its finished output, in MKVToolNix's own GUI (`mkvtoolnix-gui.exe`, from that same folder) as the run goes — a quick side-by-side look without hunting for the files afterward. The original opens first, as soon as its remux succeeds and before anything is deleted; the finished output opens afterward, once its retagging is fully done. Off by default, and only needs `mkvtoolnix-gui.exe` to actually exist in the folder when it's turned on. The console equivalent is `--open-in-mkvtoolnix-gui` (plus `--mkvtoolnix-gui <path>` if it's not alongside `--mkvmerge`/`--mkvpropedit`).

## Selecting files to process (GUI)

Every entry in **Files To Process** has its own checkbox — uncheck one to leave it out of the run without moving it out of the folder or touching the skip fragment. A file already excluded by the skip fragment shows its checkbox locked unchecked, since that rule is meant to be unconditional; toggling it there does nothing. Unchecked files survive a live refresh (the folder watcher picking up an unrelated change elsewhere) by full path, so they don't silently get reselected out from under you.

Typing into **Trim Name After**, **Remove From Name**, or **Replace In Name** updates the list live: a file any of the three would actually rename shows its new name in parentheses right next to it —

```
Lanterns.2026.S01E07.1080p.x265-ELiTE.mkv  (Lanterns 2026 S01E07 1080p x265-ELiTE.mkv)
Movie.2024.1080p.BluRay-GRP.mkv
```

— so you can see the effect of a pattern before running anything, not just after, and (for Replace In Name especially) confirm that a trailing space you can't actually see in the text box landed where you meant it to. A file none of the three settings touches shows no parenthetical at all, and a skip-fragment match shows `(skip)` instead, never both. While Trim Name After holds something `IsValidPattern` would reject outright (`***`, say), no hint is shown for *any* file — that pattern would be refused at Start, so a preview of what it would otherwise have done would be misleading.

## Parallel processing

By default files are processed one at a time on the console (`--parallel <n>` raises that); the GUI's **Settings → General → Parallel Files** spinner defaults to 2 instead, since that's a safe starting point for most setups. Try 2–4 and see what your disk can take — remuxing is I/O-bound, so a spinning HDD may get *slower*, not faster, with more than one file writing to it at once. Correctness doesn't change: the same collision guard, audio and duration safety checks apply regardless of how many files run at once. Two things do change above 1:

- **Log lines interleave.** Every stage line names its own file (`[Show.S01E07.mkv] -> remux (25%)`), so the log stays readable, just no longer strictly one file at a time top to bottom.
- **A same-run naming collision is decided by timing, not alphabetical order.** Sequentially, the first file alphabetically always wins a collision. In parallel, whichever file's inspect stage happens to finish first wins — still exactly one winner, the rest still refused and reported as failures, just no longer predictable in advance.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download) or newer to build (a newer SDK builds the `net9.0` targets fine).
- MKVToolNix — `mkvmerge.exe` and `mkvpropedit.exe` (and `mkvtoolnix-gui.exe`, if you use **Verify In MKVToolNix**). The console defaults to `%LOCALAPPDATA%\mkvtoolnix\`; the GUI asks for the install folder once and remembers it instead (see [Tool paths (GUI)](#tool-paths-gui)). All are validated before a run starts.
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
  --primary-language <c> Audio language to flag as default. Default: eng, if kept
  --include-commentary   Keep commentary-named audio tracks. Default: excluded
  --trim-start <dur>     Cut this much off the start, e.g. intros/recaps. Default: none
  --trim-end <duration>  Cut this much off the end, e.g. credits. Default: none
  --trim-name <pattern>  Cut the file name short after this pattern. Default: none
  --remove-from-name <s> Comma-separated text to delete from the file name. Default: none
  --replace-in-name <s>  Comma-separated find=replace pairs for the file name. Default: none
  --parallel <n>         Process this many files at once. Default: 1 (sequential)
  --skip <fragment>      Path fragment to skip. Default: \Processing\
  --file-formats <exts>  Comma-separated file extensions to process. Default: mkv,mp4,avi
  --no-subfolders        Process only the input folder itself
  --maintain-structure   Recreate input subfolders under --output
  --dry-run              Show what would happen without changing files
  --help                 Show this help
```

Exit code is `0` when every file succeeded, `1` otherwise.

Any file whose path contains the skip fragment (`\Processing\` by default) is passed over — handy for pointing the converter at a library that has an active download or staging folder inside it.

Only files with an extension in `--file-formats` (console) or **Settings → General → File Formats** (GUI) are even considered — `.mkv`, `.mp4` and `.avi` by default. Add more (`mkv,mp4,avi,mov,webm`) to widen it, restrict it to just the one you care about, or clear the field entirely to stop filtering by extension and consider every file in the folder — that last option is mostly useful for pointing at an already-narrow folder, since mkvmerge will simply fail (and report a failure, not a crash) on anything that isn't actually a media container it can read.

## GUI

`Media MKV Converter.Gui` is a WinForms shell over the same engine: input and output folder pickers, a preset picker, and — under **Show Settings** — the same options the console exposes, grouped into labeled sections rather than one long list:

- **General** — the MKVToolNix folder, Verify In MKVToolNix, the skip fragment, which file formats to process, and run-behavior toggles (overwrite, dry run, subfolder traversal, folder-structure mirroring, Parallel Files).
- **Tracks** — languages to keep, primary language, and commentary-track exclusion.
- **Video Trimming** — Trim From Start and Trim From End.
- **File Naming** — Trim Name After, Remove From Name, and Replace In Name.

Progress lines are colour-coded by event kind.

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
