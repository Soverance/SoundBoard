using System;
using System.Collections.Generic;
using Godot;

namespace Soverance.SoundBoard.Demo;

/// <summary>
/// A host that needs no game: generated tones on plain players. It is the smallest complete example of
/// <see cref="SoundBoardHost"/> (open <c>demo/demo_board.tscn</c>), and what CI runs <c>--board-test</c> against.
/// </summary>
public partial class DemoHost : SoundBoardHost
{
    private const int Rate = 48000;
    private const string Quiet = "Quiet";
    private const string Busy = "Busy";

    private readonly List<BoardEntry> _entries = [];
    private readonly Dictionary<string, Sound> _sounds = [];
    private readonly Random _random = new(7);
    private AudioStreamPlayer _bed = null!;
    private Sound? _held;
    private double _busyIn = -1.0;

    /// <summary>One entry's shipped tone, what plays now, and its players.</summary>
    private sealed class Sound
    {
        public required AudioStreamWav Shipped { get; init; }
        public required float ShippedDb { get; init; }
        public IReadOnlyList<AudioStream> Takes { get; set; } = [];
        public float VolumeDb { get; set; }
        public required AudioStreamPlayer[] Players { get; init; }
        public int Next { get; set; }
        public int Plays { get; set; }
        public bool Loop { get; init; }
    }

    public override BoardConfig Config { get; } = new()
    {
        ReviewPath = "user://sound_board_demo/review.json",
        AuditionRoot = "user://sound_board_demo/audition",
        SourcesPath = "",
        Title = "SOUND BOARD · DEMO",
    };

    public override IReadOnlyList<BoardEntry> Entries => _entries;

    public override IReadOnlyList<string> Contexts { get; } = [Quiet, Busy];

    public override void _Ready()
    {
        Add(new BoardEntry("ui:click", "Menus", "Click", "The cursor moves to another button."), Tone(1200f, 0.03f, 60f), -12f);
        Add(new BoardEntry("ui:confirm", "Menus", "Confirm", "A button is pressed."), Tone(660f, 0.25f, 10f), -10f);
        Add(new BoardEntry("sfx:thud", "World", "Thud", "Something heavy lands.") { AuditionKey = "thud" }, Tone(70f, 0.4f, 8f), -6f);
        Add(new BoardEntry("sfx:chime", "World", "Chime", "A reward is picked up."), Tone(880f, 0.8f, 4f), -10f);
        Add(new BoardEntry("loop:hum", "World", "Hum", "The machine room's air (it plays until stopped).") { CanBurst = false },
            Tone(110f, 1f, 0f, loop: true), -18f, loop: true);
        _entries.Add(new BoardEntry("event:door", "World", "Door opens", "A door slides open. Nothing plays today.")
        {
            Silent = true,
            TakesFile = false,
            CanBurst = false,
        });
        _bed = new AudioStreamPlayer { Stream = Tone(55f, 2f, 0f, loop: true), VolumeDb = -24f };
        AddChild(_bed);
    }

    public override void _Process(double delta)
    {
        if (_busyIn >= 0.0 && (_busyIn -= delta) < 0.0)
        {
            Fire(_sounds["sfx:thud"]);
            _busyIn = 0.4 + _random.NextDouble() * 0.6;
        }
    }

    public override string ContextFor(BoardEntry entry, string current) => entry.Category == "Menus" ? Quiet : current;

    public override IReadOnlyList<AudioStream> ShippedTakes(BoardEntry entry) =>
        _sounds.TryGetValue(entry.Id, out Sound? sound) ? [sound.Shipped] : [];

    public override void Preview(BoardEntry entry, IReadOnlyList<AudioStream> takes, bool shipped, float offsetDb)
    {
        if (_sounds.TryGetValue(entry.Id, out Sound? sound))
        {
            sound.Takes = takes;
            sound.VolumeDb = sound.ShippedDb + offsetDb;
        }
    }

    public override void Play(BoardEntry entry, PlayMode mode)
    {
        if (mode != PlayMode.BurstShot)
        {
            Release();
        }
        if (!_sounds.TryGetValue(entry.Id, out Sound? sound))
        {
            return;
        }
        Fire(sound);
        if (sound.Loop)
        {
            _held = sound;
        }
    }

    public override void StopAll()
    {
        StopContext();
        foreach (Sound sound in _sounds.Values)
        {
            foreach (AudioStreamPlayer player in sound.Players)
            {
                player.Stop();
            }
        }
        _held = null;
    }

    public override void StartContext(string context)
    {
        _bed.Play();
        _busyIn = context == Busy ? 0.2 : -1.0;
    }

    public override void StopContext()
    {
        _bed.Stop();
        _busyIn = -1.0;
    }

    public override string Describe(BoardEntry entry, float offsetDb) =>
        _sounds.TryGetValue(entry.Id, out Sound? sound)
            ? $"A generated tone · {sound.ShippedDb + offsetDb:+0;-0;0} dB · {sound.Players.Length} voices"
            : "Nothing plays.";

    public override string Status() => _bed.Playing ? $"Context: {(_busyIn >= 0.0 ? Busy : Quiet)}" : "Context: off";

    public override int Plays(BoardEntry entry) => _sounds.TryGetValue(entry.Id, out Sound? sound) ? sound.Plays : -1;

    public override void SelfTest(BoardTestReport report) =>
        report.Check("demo", _entries.Count == 6 && _sounds.Count == 5, $"{_entries.Count}");

    private void Add(BoardEntry entry, AudioStreamWav tone, float volumeDb, bool loop = false)
    {
        var players = new AudioStreamPlayer[loop ? 1 : 3];
        for (int i = 0; i < players.Length; i++)
        {
            players[i] = new AudioStreamPlayer();
            AddChild(players[i]);
        }
        _sounds[entry.Id] = new Sound { Shipped = tone, ShippedDb = volumeDb, Takes = [tone], VolumeDb = volumeDb, Players = players, Loop = loop };
        _entries.Add(entry);
    }

    private void Fire(Sound sound)
    {
        if (sound.Takes.Count == 0)
        {
            return;
        }
        AudioStreamPlayer player = sound.Players[sound.Next];
        sound.Next = (sound.Next + 1) % sound.Players.Length;
        player.Stream = sound.Takes[_random.Next(sound.Takes.Count)];
        player.VolumeDb = sound.VolumeDb;
        player.Play();
        sound.Plays++;
    }

    private void Release()
    {
        if (_held is null)
        {
            return;
        }
        foreach (AudioStreamPlayer player in _held.Players)
        {
            player.Stop();
        }
        _held = null;
    }

    /// <summary>A sine of <paramref name="seconds"/> that decays at <paramref name="decay"/> per second (0 holds), 16-bit
    /// mono; a loop repeats the whole of it.</summary>
    private static AudioStreamWav Tone(float hz, float seconds, float decay, bool loop = false)
    {
        int frames = (int)(Rate * seconds);
        byte[] data = new byte[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            float t = (float)i / Rate;
            float fade = Math.Min(1f, i / 96f) * Math.Min(1f, (frames - i) / 96f);
            float s = 0.6f * MathF.Exp(-decay * t) * fade * MathF.Sin(MathF.Tau * hz * t);
            short v = (short)(Math.Clamp(s, -1f, 1f) * 32767f);
            data[2 * i] = (byte)(v & 0xFF);
            data[2 * i + 1] = (byte)((v >> 8) & 0xFF);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Stereo = false,
            Data = data,
            LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopEnd = loop ? frames : 0,
        };
    }
}
