# Sound Board for Godot

**A Godot 4 .NET editor plugin for judging every sound in a game by ear, and leaving the verdicts in a file that a
coding agent (or a teammate) applies.**

It was built for working with an AI coding agent that cannot hear. The agent short-lists and cuts candidate sounds;
a person opens the board from the editor's toolbar, hears each sound exactly as the game plays it (alone, over a
moment of the game, or ten at once), steps through the candidates, nudges the level, and clicks **Keep** or
**Replace**. Each click is saved to `review.json`, which the agent reads and applies. Nothing is copied and pasted,
and the board never writes a game file.

> The Sound Board is an internal Soverance Studios tool shared as-is. It is not a supported product: there is no
> support commitment and no release cadence.

## What you get

- **A toolbar button** that runs your project's board scene.
- **The board:** every sound grouped by category, each with its status (*unheard*, *kept*, *replace*, *re-hear*,
  *pick*, *done*) and a mark when its licence needs confirming. The detail pane shows when it plays, how the game
  plays it, a waveform, its candidates and its level.
- **SOLO**, **IN CONTEXT** (over a moment the game defines: a level's ambience, a fight) and **BURST** (ten plays in
  200 ms, to hear what voice limits do in a crowd).
- **Candidates** from an audition folder, and **USE A FILE…** to hear any WAV, OGG or MP3 on disk in a sound's place.
- **Rounds:** start a new pass after a playtest; everything judged before reads *re-hear* until it is judged again.
- **`--board-test`**, a headless check of the board, the review file and your game's coverage, in one
  `[board-test] PASS|FAIL …` line with exit code 0 or 1.

Every button shows its key, and every key is optional: Space, C, B, Esc, `[` `]`, `-` `=`, K, R, F, N.

## Requirements

Godot **4.7** with .NET (C#), `net8.0`. Windows and Linux.

## Install

Mount the repository at `addons/sound_board` (a git submodule keeps one copy shared between games):

```
git submodule add https://github.com/Soverance/SoundBoard.git addons/sound_board
```

Then enable **Sound Board** in Project Settings › Plugins, or add it to `project.godot` with the editor closed:

```ini
[editor_plugins]

enabled=PackedStringArray("res://addons/sound_board/plugin.cfg")
```

Godot compiles the plugin's C# into your game's assembly, so there is nothing else to build. Add
`addons/sound_board/*`, your board scene and `review.json` to your export presets' exclude filter.

## Make your game's board

A board scene is a `BoardScreen` with one child node that derives from `SoundBoardHost`. The toolbar button runs
`res://tools/audio/sound_board.tscn`; to keep yours elsewhere, set it in `project.godot`:

```ini
[sound_board]

scene="res://dev/sound_board/sound_board.tscn"
```

The scene:

```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://addons/sound_board/ui/BoardScreen.cs" id="1"]
[ext_resource type="Script" path="res://dev/sound_board/MyBoardHost.cs" id="2"]

[node name="SoundBoard" type="Control"]
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
script = ExtResource("1")

[node name="Host" type="Node" parent="."]
script = ExtResource("2")
```

The host builds your game's audio in `_Ready` (children are ready before the screen) and answers the board:

| Member | What the board needs |
|---|---|
| `Config` | Paths: `ReviewPath`, `AuditionRoot`, `SourcesPath`; `LibraryRoot`, where USE A FILE… opens; the title and help line. |
| `Entries` | Every sound, in list order: id, category, label, "plays when", audition key, and flags (`Silent`, `TakesFile`, `CanBurst`, `SitsOutRounds`). Include the moments that are silent by design, so a missing sound is a decision. |
| `ShippedTakes(entry)` | The takes the game ships: the first candidate, and the waveform's source. |
| `Preview(entry, takes, shipped, offsetDb)` | From now on, play these takes at the shipped level plus the offset. In memory only. |
| `Play(entry, mode)`, `StopAll()` | Play it the way the game does: through your own mixer, voice limits and buses, so it sounds as the player hears it. |
| `Contexts`, `ContextFor`, `StartContext`, `StopContext` | The moments IN CONTEXT plays over. Optional. |
| `Describe`, `Status` | The "Now:" line (level, voices, bus, where it is set) and the header's status. Optional. |
| `Plays(entry)`, `SelfTest(report)` | A play counter and your own checks, for `--board-test`. Check coverage here: every sound your game can make is an entry. |
| `Style` | Colours, to match your game. Optional. |

`demo/DemoHost.cs` is a complete host that plays generated tones; open `demo/demo_board.tscn` to try the board
without a game.

## Candidates

An entry's candidates live in `<AuditionRoot>/<audition key>/`, in either layout:

- **A folder** is one candidate, and every sound file in it is a take (one plays per hit).
- **Loose files** that differ only by a trailing `_N` are the takes of one candidate (`walk_1.wav` and `walk_2.wav`
  are `walk`). A lone file is a candidate under its whole name (`impact_07.wav` is `impact_07`).

Imported files play through their import settings. A file not imported yet plays straight from disk.

## `review.json`

Saved on every click, written through a temporary file so a crash can't leave half a file. It is meant to be
committed, so every verdict has a diff. Entries are sorted by id.

```json
{
  "version": 1,
  "round": { "number": 2, "label": "after the playtest", "started": "2026-10-20T21:00:00" },
  "entries": {
    "cue:sword_hit": { "verdict": "replace", "candidate": "axe_flesh_hit", "levelDb": -2, "note": "meatier", "at": "2026-10-20T21:14:03" },
    "cue:step": { "verdict": "keep", "levelDb": 0, "note": "", "at": "2026-10-20T21:15:40", "applied": "kept; 2026-10-21" },
    "event:MoveUndone": { "verdict": "silentOk", "levelDb": 0, "note": "", "at": "2026-10-20T21:16:02" }
  }
}
```

- `verdict`: `keep`, `replace`, `needsSound` or `silentOk` (the last two for an entry that plays nothing).
- `candidate`: the audition candidate chosen, or absent for the shipped sound. `file`: a file picked from disk.
  `levelDb` is relative to the shipped level.
- `applied`: whoever applies a verdict writes what they did and when. Only an entry without `applied` is work. A
  new verdict clears it. A Keep that changes nothing only re-stamps `at`, so a round's unchanged sounds are no work.
- A `replace` or `needsSound` with no candidate means "find me something". Once candidates are cut and `applied`
  says so, the board lists it as **pick**.

## Command line

- `<godot> --headless --path . res://path/to/board.tscn -- --board-test` checks the review file round trip, rounds,
  the licence reader, both candidate layouts, that every entry reaches the game (one per frame), the screen's
  verdict flow and burst, then your `SelfTest`. It writes only under `user://sound_board_test/`.
- `<godot> --path . res://path/to/board.tscn -- --shot=board.png [--shot-frames=45]` saves a screenshot and quits.

## Development

CI builds the plugin inside a fresh Godot project (`.github/ci/`), with warnings as errors, and runs the demo's
`--board-test` headless on Linux. Keep the repository free of audio, game names and site-specific paths. Commit
Godot's `.uid` files, so a game's editor leaves the submodule clean.

Planned: each sound's record from [Foley](https://github.com/Soverance/Foley) (source, loudness) in the detail pane,
"open in Foley", and "more like this" requests.

## Licence

MIT. See [LICENSE](LICENSE).
