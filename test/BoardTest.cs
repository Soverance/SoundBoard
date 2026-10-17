using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace Soverance.SoundBoard;

/// <summary>
/// <c>--board-test</c>: the board's own checks over the running screen and its host, then the host's, printed as one
/// <c>[board-test] PASS|FAIL …</c> line; quits 0 on a pass, 1 on a fail. It writes only under
/// <see cref="BoardScreen.TestDir"/> (never the real review file), with fixture tones it generates itself.
/// </summary>
public partial class BoardTest : Node
{
    public BoardScreen Screen { get; init; } = null!;

    private readonly BoardTestReport _report = new();
    private readonly Queue<BoardEntry> _toPlay = new();
    private int _played;
    private string? _silentEntry;
    private bool _started;
    private BoardEntry? _burstEntry;
    private int _burstBefore;
    private double _burstWait = -1.0;

    public override void _Process(double delta)
    {
        if (!_started)
        {
            _started = true;
            Run("entries", Entries);
            Run("review", ReviewRoundTrip);
            Run("rounds", Rounds);
            Run("licence", Licence);
            Run("layouts", Layouts);
            foreach (BoardEntry entry in Screen.Entries)
            {
                if (!entry.Silent && Screen.Host.Plays(entry) >= 0)
                {
                    _toPlay.Enqueue(entry);
                }
            }
            if (_toPlay.Count == 0)
            {
                RunRest();
            }
            return;
        }
        // One sound a frame, as a reviewer plays them: a game may allow only one UI sound per frame.
        if (_toPlay.TryDequeue(out BoardEntry? next))
        {
            PlayOne(next);
            if (_toPlay.Count == 0)
            {
                Screen.StopAll();
                _report.Check("plays", _silentEntry is null, _silentEntry is null ? $"{_played}/{Screen.Entries.Count}" : $"silent:{_silentEntry}");
                RunRest();
            }
            return;
        }
        if (_burstWait < 0.0 || (_burstWait -= delta) > 0.0)
        {
            return;
        }
        _burstWait = -1.0;
        if (_burstEntry is not null)
        {
            int plays = Screen.Host.Plays(_burstEntry) - _burstBefore;
            _report.Check("burst", plays >= 1, $"{_burstEntry.Id}:{plays}");
        }
        Screen.StopAll();
        Finish();
    }

    private void Run(string name, Func<(bool Pass, string Detail)> check)
    {
        try
        {
            (bool pass, string detail) = check();
            _report.Check(name, pass, detail);
        }
        catch (Exception e)
        {
            _report.Check(name, false, $"{e.GetType().Name}:{e.Message}");
        }
    }

    private (bool, string) Entries()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (BoardEntry entry in Screen.Entries)
        {
            if (!ids.Add(entry.Id))
            {
                return (false, $"duplicate:{entry.Id}");
            }
            if (entry.Label.Length == 0 || entry.Category.Length == 0 || entry.When.Length == 0)
            {
                return (false, $"blank:{entry.Id}");
            }
        }
        return (Screen.Entries.Count > 0, Screen.Entries.Count.ToString());
    }

    private static (bool, string) ReviewRoundTrip()
    {
        string path = ProjectSettings.GlobalizePath(BoardScreen.TestDir + "/roundtrip.json");
        var data = new ReviewData();
        data.Entries["b"] = new ReviewEntry { Verdict = Verdicts.Replace, Candidate = "x", LevelDb = -2f, Note = "a -> b +2", At = "2026-01-02T03:04:05" };
        data.Entries["a"] = new ReviewEntry { Verdict = Verdicts.Keep, At = "2026-01-02T03:04:06", Applied = "done" };
        ReviewFile.Save(path, data);
        string first = File.ReadAllText(path);
        ReviewData back = ReviewFile.Load(path, out string? error);
        ReviewFile.Save(path, back);
        if (error is not null || File.ReadAllText(path) != first)
        {
            return (false, "unstable");
        }
        if (first.Contains('\r') || !first.Contains("a -> b +2") || first.IndexOf("\"a\"", StringComparison.Ordinal) > first.IndexOf("\"b\"", StringComparison.Ordinal))
        {
            return (false, "format");
        }
        if (first.Contains("round", StringComparison.Ordinal))
        {
            return (false, "round-written");
        }
        File.WriteAllText(path, "{ \"entries\": { \"a\": null } }");
        ReviewData bad = ReviewFile.Load(path, out error);
        bool kept = File.Exists(path + ".bad");
        return error is not null && kept && bad.Entries.Count == 0 ? (true, "ok") : (false, "malformed");
    }

    private static (bool, string) Rounds()
    {
        var keep = new BoardEntry("t:keep", "T", "Keep", "x");
        var open = new BoardEntry("t:open", "T", "Open", "x");
        var music = new BoardEntry("t:music", "T", "Music", "x") { SitsOutRounds = true };
        BoardEntry[] entries = [keep, open, music];
        var review = new ReviewData();
        review.Entries[keep.Id] = new ReviewEntry { Verdict = Verdicts.Keep, At = "2026-01-01T00:00:00" };
        review.Entries[music.Id] = new ReviewEntry { Verdict = Verdicts.Keep, At = "2026-01-01T00:00:00" };
        if (BoardRound.Next(entries, review, null) != open || BoardRound.Progress(entries, review) != "Round 1 · 2 / 3 heard · 0 replace")
        {
            return (false, "round1");
        }
        BoardRound.Start(review, " again ", "2026-02-01T00:00:00");
        if (!BoardRound.ToReHear(keep, review) || BoardRound.ToReHear(music, review)
            || BoardRound.Next(entries, review, keep) != open || BoardRound.Next(entries, review, open) != keep)
        {
            return (false, "round2");
        }
        review.Entries[open.Id] = new ReviewEntry { Verdict = Verdicts.Replace, At = "2026-02-02T00:00:00", Applied = "cut 3" };
        string progress = BoardRound.Progress(entries, review);
        return progress == "Round 2 · again · 1 / 2 re-heard · 0 replace · 1 to pick" ? (true, "ok") : (false, progress);
    }

    private static (bool, string) Licence()
    {
        SourceLicence licence = SourceLicence.FromLines([
            "| File | Source |",
            "|---|---|",
            "| `sfx/hit.wav` | a pack, royalty-free |",
            "| `sfx/odd.wav` | unknown: **Confirm** |",
            "Not a row: `sfx/other.wav` Confirm",
        ]);
        bool pass = licence.StatusFor("hit.wav") == LicenceStatus.Clean && licence.StatusFor("odd.wav") == LicenceStatus.Confirm
                    && licence.StatusFor("other.wav") == LicenceStatus.Missing;
        return (pass, pass ? "ok" : "wrong");
    }

    /// <summary>Both audition layouts: a folder of takes, and loose files grouped by their _N suffix.</summary>
    private (bool, string) Layouts()
    {
        string root = BoardScreen.TestDir + "/audition";
        string key = ProjectSettings.GlobalizePath(root + "/fixture");
        if (Directory.Exists(key))
        {
            Directory.Delete(key, recursive: true);
        }
        Tone(key + "/walk_1.wav", 220f);
        Tone(key + "/walk_2.wav", 247f);
        Tone(key + "/thud.wav", 110f);
        Tone(key + "/pop_07.wav", 330f);
        Tone(key + "/chime/high.wav", 880f);
        Tone(key + "/chime/low.wav", 440f);
        File.WriteAllText(key + "/notes.txt", "not audio");
        List<BoardCandidate> found = Candidates.LoadAudition(root, "fixture");
        string shape = string.Join(",", found.ConvertAll(c => $"{c.Name}:{c.Takes.Count}"));
        if (shape != "chime:2,pop_07:1,thud:1,walk:2")
        {
            return (false, shape);
        }
        Vector2[] envelope = Waveform.Envelope(found[0].Paths[0], 64);
        bool wave = envelope.Length == 64 && envelope[32].Y > 0.3f && envelope[32].X < -0.3f;
        return wave ? (true, shape) : (false, "waveform");
    }

    /// <summary>The checks after every sound has played once.</summary>
    private void RunRest()
    {
        if (_toPlay.Count == 0 && _played == 0)
        {
            _report.Check("plays", true, "uncounted");
        }
        Run("ui", Ui);
        Run("host", () =>
        {
            Screen.Host.SelfTest(_report);
            return (true, "ok");
        });
        Screen.StopAll();
        StartBurst();
    }

    /// <summary>The entry, played SOLO from the screen, reaches the game.</summary>
    private void PlayOne(BoardEntry entry)
    {
        int before = Screen.Host.Plays(entry);
        Screen.Select(entry);
        Screen.Solo();
        if (Screen.Host.Plays(entry) > before)
        {
            _played++;
        }
        else
        {
            _silentEntry ??= entry.Id;
        }
    }

    /// <summary>Drives the screen as a reviewer would, on the test review file.</summary>
    private (bool, string) Ui()
    {
        foreach (BoardEntry entry in Screen.Entries)
        {
            Screen.Select(entry);
            if (Screen.Host.Contexts.Count > 0)
            {
                Screen.InContext();
            }
        }
        Screen.StopAll();
        BoardEntry? sound = Find(e => !e.Silent);
        if (sound is null)
        {
            return (false, "no-sound");
        }
        Screen.Select(sound);
        if (Screen.List.StatusOf(sound) != "unheard")
        {
            return (false, "not-unheard");
        }
        Screen.Judge(true);
        if (Screen.List.StatusOf(sound) != "kept" || !File.Exists(Screen.ReviewPath))
        {
            return (false, "keep");
        }
        Screen.StepLevel(1f);
        Screen.Judge(false);
        ReviewEntry saved = ReviewFile.Load(Screen.ReviewPath, out _).Entries[sound.Id];
        if (Screen.List.StatusOf(sound) != "replace" || saved.Verdict != Verdicts.Replace || saved.LevelDb != 1f)
        {
            return (false, "replace");
        }
        Screen.StepLevel(-1f);
        if (Screen.Candidates.For(sound).Count > 1)
        {
            Screen.StepCandidate(1);
            if (ReviewFile.Load(Screen.ReviewPath, out _).Entries[sound.Id].Candidate != Screen.Candidates.For(sound)[1].Name)
            {
                return (false, "candidate");
            }
            Screen.StepCandidate(-1);
        }
        if (Find(e => e.Silent) is { } silent)
        {
            Screen.Select(silent);
            if (Screen.List.StatusOf(silent) != "SILENT")
            {
                return (false, "silent");
            }
            Screen.Judge(true);
            if (Screen.List.StatusOf(silent) != "silent ok")
            {
                return (false, "silent-ok");
            }
        }
        // Stamps are to the second: date the verdicts back so the round starts after them.
        foreach (ReviewEntry verdict in Screen.Review.Entries.Values)
        {
            verdict.At = "2000-01-01T00:00:00";
        }
        Screen.StartRound("test");
        if (Screen.List.StatusOf(sound) != "re-hear" || !Screen.ProgressText.StartsWith("Round 2 · test", StringComparison.Ordinal))
        {
            return (false, "round");
        }
        return (true, "ok");
    }

    private void StartBurst()
    {
        _burstEntry = Find(e => e.CanBurst && !e.Silent && Screen.Host.Plays(e) >= 0);
        if (_burstEntry is null)
        {
            _report.Check("burst", true, "none");
            Finish();
            return;
        }
        Screen.Select(_burstEntry);
        _burstBefore = Screen.Host.Plays(_burstEntry);
        Screen.Burst();
        _burstWait = 0.5;
    }

    private BoardEntry? Find(Predicate<BoardEntry> match)
    {
        foreach (BoardEntry entry in Screen.Entries)
        {
            if (match(entry))
            {
                return entry;
            }
        }
        return null;
    }

    private void Finish()
    {
        GD.Print(_report.ToString());
        GetTree().Quit(_report.Passed ? 0 : 1);
    }

    /// <summary>A 0.2 s sine at -6 dBFS.</summary>
    private static void Tone(string absPath, float hz)
    {
        const int rate = 48000;
        var samples = new float[rate / 5];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.5f * MathF.Sin(MathF.Tau * hz * i / rate);
        }
        Wav.Write16(absPath, samples, 1, rate);
    }
}
