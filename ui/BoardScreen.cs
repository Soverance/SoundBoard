using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;

namespace Soverance.SoundBoard;

/// <summary>
/// The sound board: a reviewer hears every sound in the game as a player would, alone or in a context, compares
/// candidates, and leaves verdicts in <c>review.json</c> for the agent. The game comes in through the
/// <see cref="SoundBoardHost"/> child. It never writes a game file: previews apply in memory only.
/// <para><c>--board-test</c> runs its checks and the host's and quits (0 pass, 1 fail), saving only to
/// <c>user://</c>. <c>--shot=PATH</c> saves a PNG after <c>--shot-frames=N</c> frames (default 45) and quits.</para>
/// </summary>
public partial class BoardScreen : Control
{
    public const string TestDir = "user://sound_board_test";
    public const string TestReviewPath = TestDir + "/review.json";

    private const int WaveColumns = 480;
    private const int BurstCount = 10;
    private const double BurstSeconds = 0.2;

    public SoundBoardHost Host { get; private set; } = null!;
    public Candidates Candidates { get; private set; } = null!;
    public BoardList List { get; private set; } = null!;
    public BoardDetail Detail { get; private set; } = null!;
    public IReadOnlyList<BoardEntry> Entries { get; private set; } = [];
    public string ReviewPath { get; private set; } = "";
    public ReviewData Review { get; private set; } = new();
    public string? ReviewError { get; private set; }
    public BoardEntry Current { get; private set; } = null!;

    /// <summary>The context running now, or null.</summary>
    public string? ActiveContext { get; private set; }

    public string ProgressText => _progress.Text;

    public ConfirmationDialog RoundDialog => _roundDialog;

    private readonly Dictionary<string, Vector2[]> _envelopes = [];
    private BoardConfig _config = null!;
    private BoardStyle _style = null!;
    private SourceLicence _licence = null!;
    private string _reviewName = "";
    private string? _fileDir;
    private OptionButton _context = null!;
    private Label _status = null!;
    private Label _progress = null!;
    private ConfirmationDialog _roundDialog = null!;
    private LineEdit _roundLabel = null!;
    private double _statusRefresh;
    private BoardEntry? _burst;
    private int _burstLeft;
    private double _burstIn;
    private string? _shotPath;
    private int _shotFrames;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        if (FindHost() is not { } host)
        {
            AddChild(BoardText.Make("This board scene needs a child node that derives from SoundBoardHost.", 16, BoardStyle.Default.Warn));
            GD.PushError("[sound-board] no SoundBoardHost child");
            return;
        }
        Host = host;
        _config = Host.Config;
        _style = Host.Style;
        bool test = HasArg("--board-test");
        if (!test && DisplayServer.GetName() != "headless")
        {
            // A game's options may say fullscreen; the board sits beside the editor.
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        }
        ReviewPath = ProjectSettings.GlobalizePath(test ? TestReviewPath : _config.ReviewPath);
        _reviewName = Path.GetFileName(ReviewPath);
        if (test && File.Exists(ReviewPath))
        {
            File.Delete(ReviewPath);
        }
        Review = ReviewFile.Load(ReviewPath, out string? error);
        ReviewError = error;
        _licence = SourceLicence.Load(_config.SourcesPath);
        Entries = Host.Entries;
        Candidates = new Candidates(Host, _config.AuditionRoot);

        var background = new ColorRect { Color = _style.Background, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);
        MoveChild(background, 0);
        BuildLayout();

        if (Entries.Count == 0)
        {
            Detail.ShowSaved("The host lists no sounds.");
        }
        else
        {
            List.Build(Entries, Review, NeedsConfirm);
            Select(BoardRound.Next(Entries, Review, null) ?? Entries[0]);
        }
        _progress.Text = BoardRound.Progress(Entries, Review);
        if (ReviewError is not null)
        {
            Detail.ShowSaved(ReviewError);
        }
        List.GrabFocus();

        if (test)
        {
            AddChild(new BoardTest { Screen = this });
        }
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--shot=", StringComparison.Ordinal))
            {
                _shotPath = arg["--shot=".Length..];
            }
            else if (arg.StartsWith("--shot-frames=", StringComparison.Ordinal) && int.TryParse(arg["--shot-frames=".Length..], out int frames))
            {
                _shotFrames = frames;
            }
        }
        if (_shotPath is not null && _shotFrames <= 0)
        {
            _shotFrames = 45;
        }
    }

    public override void _ExitTree() => Host?.StopAll();

    public override void _Process(double delta)
    {
        if (Host is null)
        {
            return;
        }
        if (_burst is { } entry && (_burstIn -= delta) <= 0.0)
        {
            _burstIn += BurstSeconds / BurstCount;
            Host.Play(entry, PlayMode.BurstShot);
            if (--_burstLeft == 0)
            {
                _burst = null;
            }
        }
        if ((_statusRefresh -= delta) <= 0.0)
        {
            _statusRefresh = 0.25;
            _status.Text = Host.Status();
        }
        if (_shotPath is not null && --_shotFrames <= 0)
        {
            SaveShot(_shotPath);
            _shotPath = null;
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Typing a note is typing, not shortcuts; so is naming a round (its dialog is a window with its own focus).
        if (Host is null || Entries.Count == 0 || @event is not InputEventKey { Pressed: true } key
            || GetViewport().GuiGetFocusOwner() is LineEdit || _roundDialog.Visible)
        {
            return;
        }
        switch (key.PhysicalKeycode)
        {
            case Key.Space when !key.Echo:
                Solo();
                break;
            case Key.C when !key.Echo:
                InContext();
                break;
            case Key.B when !key.Echo:
                Burst();
                break;
            case Key.Escape when !key.Echo:
                StopAll();
                break;
            case Key.Bracketleft when !key.Echo:
                StepCandidate(-1);
                break;
            case Key.Bracketright when !key.Echo:
                StepCandidate(1);
                break;
            case Key.Minus:
                StepLevel(-1f);
                break;
            case Key.Equal:
                StepLevel(1f);
                break;
            case Key.K when !key.Echo:
                Judge(true);
                break;
            case Key.R when !key.Echo:
                Judge(false);
                break;
            case Key.F when !key.Echo:
                ChooseFile();
                break;
            case Key.N when !key.Echo:
                NextToReHear();
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    public void Select(BoardEntry entry)
    {
        Current = entry;
        List.Pick(entry);
        RestorePreview(entry);
        (int candidate, float offset) = Candidates.PreviewOf(entry);
        BoardCandidate chosen = Candidates.For(entry)[candidate];
        string path = chosen.Paths.Count > 0 ? chosen.Paths[0] : "";
        string licence = chosen.File is not null
            ? "Licence: a file from disk. Say where it's from in the note if it isn't your library or a pack you bought."
            : LicenceText(path.GetFile());
        Detail.ShowEntry(entry, Host.Describe(entry, offset), licence, EnvelopeOf(path), Host.Contexts.Count > 0,
                         Candidates.For(entry).Count > 1, entry.TakesFile && !entry.Silent);
        ShowPreview();
        ShowVerdict();
        if (Host.Contexts.Count > 0)
        {
            string wanted = Host.ContextFor(entry, SelectedContext());
            int index = IndexOf(Host.Contexts, wanted);
            if (index >= 0 && index != _context.Selected)
            {
                _context.Select(index);
            }
        }
    }

    /// <summary>A verdict saved in an earlier session and not yet applied plays as it was left, so a nudge builds on it
    /// rather than on the shipped sound. An applied one is already in the shipped sound.</summary>
    private void RestorePreview(BoardEntry entry)
    {
        if (Candidates.HasPreview(entry) || !Review.Entries.TryGetValue(entry.Id, out ReviewEntry? verdict) || verdict.Applied is not null)
        {
            return;
        }
        int candidate = 0;
        if (verdict.File is { } path)
        {
            candidate = Math.Max(0, Candidates.AddFile(entry, path, out _));
        }
        else
        {
            IReadOnlyList<BoardCandidate> list = Candidates.For(entry);
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].Name == verdict.Candidate)
                {
                    candidate = i;
                }
            }
        }
        Candidates.SetPreview(entry, candidate, verdict.LevelDb);
    }

    /// <summary>USE A FILE…: hear a WAV, OGG or MP3 from disk as the current sound (the dialog's pick lands here).
    /// False, with the reason on screen, when it won't load or the sound can't take a file.</summary>
    public bool UseFile(string path)
    {
        int candidate = Candidates.AddFile(Current, path, out string? error);
        if (candidate < 0)
        {
            Detail.ShowSaved(error ?? "That file can't be used here.");
            return false;
        }
        _fileDir = Path.GetDirectoryName(path);
        Candidates.SetPreview(Current, candidate, Candidates.PreviewOf(Current).OffsetDb);
        Select(Current);
        FollowVerdict();
        return true;
    }

    /// <summary>Opens the OS file picker, starting where the last file came from, else the configured library.</summary>
    private void ChooseFile()
    {
        if (!Current.TakesFile || Current.Silent)
        {
            return;
        }
        var dialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Title = $"A file for {Current.Label}",
            Filters = ["*.wav, *.ogg, *.mp3 ; Audio files"],
        };
        if ((_fileDir ?? _config.LibraryRoot) is { Length: > 0 } start)
        {
            dialog.CurrentDir = start;
        }
        dialog.FileSelected += path =>
        {
            UseFile(path);
            dialog.QueueFree();
        };
        dialog.Canceled += dialog.QueueFree;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(960, 640));
    }

    private void BuildLayout()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "top", "right", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", 16);
        }
        AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margin.AddChild(column);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 16);
        column.AddChild(header);
        header.AddChild(BoardText.Make(_config.Title, 18, _style.Title));
        Label help = BoardText.Make(_config.Help, 14, _style.TextDim);
        help.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(help);
        _context = new OptionButton
        {
            FocusMode = FocusModeEnum.None,
            TooltipText = "The moment IN CONTEXT plays over.",
        };
        foreach (string context in Host.Contexts)
        {
            _context.AddItem(context.ToUpperInvariant());
        }
        _context.ItemSelected += _ =>
        {
            if (ActiveContext is not null)
            {
                StartContext(SelectedContext());
            }
        };
        if (Host.Contexts.Count > 0)
        {
            header.AddChild(BoardText.Make("IN CONTEXT", 11, _style.TextDim));
            header.AddChild(_context);
        }

        _status = BoardText.Make("", 12, _style.TextDim);
        column.AddChild(_status);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 20);
        column.AddChild(body);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(440f, 0f) };
        body.AddChild(left);
        _progress = BoardText.Make("", 13, _style.Accent);
        left.AddChild(_progress);
        List = new BoardList { SizeFlagsVertical = SizeFlags.ExpandFill, Style = _style };
        left.AddChild(List);
        var rounds = new HBoxContainer();
        rounds.AddThemeConstantOverride("separation", 10);
        left.AddChild(rounds);
        rounds.AddChild(ListButton("NEXT TO RE-HEAR  [N]", "The next sound not heard this round, after this one.", NextToReHear));
        rounds.AddChild(ListButton("NEW ROUND…", "Start a fresh pass: everything you've judged reads \"re-hear\" until you judge it again.", AskNewRound));
        Detail = new BoardDetail { SizeFlagsHorizontal = SizeFlags.ExpandFill, Style = _style };
        body.AddChild(Detail);
        BuildRoundDialog();

        List.EntryPicked += Select;
        Detail.SoloPressed += Solo;
        Detail.ContextPressed += InContext;
        Detail.BurstPressed += Burst;
        Detail.StopPressed += StopAll;
        Detail.CandidateStepped += StepCandidate;
        Detail.FileRequested += ChooseFile;
        Detail.LevelStepped += StepLevel;
        Detail.VerdictPressed += Judge;
        Detail.NoteCommitted += CommitNote;
        Detail.FocusReturned += () => List.GrabFocus();
    }

    public void Solo()
    {
        CancelBurst();
        StopContext();
        Host.Play(Current, PlayMode.Solo);
    }

    public void InContext()
    {
        if (Host.Contexts.Count == 0)
        {
            return;
        }
        CancelBurst();
        string context = SelectedContext();
        if (ActiveContext != context)
        {
            StartContext(context);
        }
        Host.Play(Current, PlayMode.InContext);
    }

    /// <summary>Ten plays in 200 ms: what the voice limits do in a crowd.</summary>
    public void Burst()
    {
        if (!Current.CanBurst)
        {
            return;
        }
        _burst = Current;
        _burstLeft = BurstCount;
        _burstIn = 0.0;
    }

    public void StopAll()
    {
        CancelBurst();
        ActiveContext = null;
        Host.StopAll();
    }

    private void StartContext(string context)
    {
        ActiveContext = context;
        Host.StartContext(context);
    }

    private void StopContext()
    {
        if (ActiveContext is null)
        {
            return;
        }
        ActiveContext = null;
        Host.StopContext();
    }

    private void CancelBurst()
    {
        _burst = null;
        _burstLeft = 0;
    }

    public void StepCandidate(int step)
    {
        int count = Candidates.For(Current).Count;
        if (count < 2)
        {
            return;
        }
        (int candidate, float offset) = Candidates.PreviewOf(Current);
        Candidates.SetPreview(Current, (candidate + step + count) % count, offset);
        Select(Current);
        FollowVerdict();
    }

    public void StepLevel(float db)
    {
        (int candidate, float offset) = Candidates.PreviewOf(Current);
        Candidates.SetPreview(Current, candidate, offset + db);
        Select(Current);
        FollowVerdict();
    }

    /// <summary>K (true) or R (false) on the current sound.</summary>
    public void Judge(bool keep)
    {
        string verdict = keep ? (Current.Silent ? Verdicts.SilentOk : Verdicts.Keep) : (Current.Silent ? Verdicts.NeedsSound : Verdicts.Replace);
        (int candidate, float offset) = Candidates.PreviewOf(Current);
        // The same Keep again on an applied verdict, hearing the shipped sound at its level with the same note, changes
        // nothing: it only re-stamps when it was heard, so a round's unchanged keeps are no work for the agent. (An
        // applied verdict's candidate and level are in the shipped sound now, so the preview starts from 0 and 0 dB.)
        if (Review.Entries.TryGetValue(Current.Id, out ReviewEntry? old) && old.Applied is not null && keep && old.Verdict == verdict
            && candidate == 0 && offset == 0f && old.Note == Detail.Note)
        {
            old.At = Stamp();
            SaveReview();
            return;
        }
        Review.Entries[Current.Id] = new ReviewEntry
        {
            Verdict = verdict,
            Candidate = candidate > 0 ? Candidates.For(Current)[candidate].Name : null,
            File = Candidates.FileOf(Current, candidate),
            LevelDb = offset,
            Note = Detail.Note,
            At = Stamp(),
        };
        SaveReview();
    }

    /// <summary>[N]: selects the next sound not heard this round, after this one, wrapping round.</summary>
    public void NextToReHear()
    {
        if (BoardRound.Next(Entries, Review, Current) is { } next)
        {
            Select(next);
        }
        else
        {
            Detail.ShowSaved($"Round {BoardRound.Number(Review)} is done: every sound in it is heard.");
        }
    }

    /// <summary>NEW ROUND…: a round changes every row, so it asks first, and for a name.</summary>
    private void AskNewRound()
    {
        int number = BoardRound.Number(Review) + 1;
        _roundDialog.Title = $"Start round {number}";
        _roundDialog.OkButtonText = $"START ROUND {number}";
        _roundLabel.Text = "";
        _roundDialog.PopupCentered();
        _roundLabel.GrabFocus();
    }

    /// <summary>Starts the next round under <paramref name="label"/> and saves it; the board moves to the first sound to
    /// re-hear.</summary>
    public void StartRound(string label)
    {
        BoardRound.Start(Review, label, Stamp());
        ReviewFile.Save(ReviewPath, Review);
        List.RefreshAll(Review);
        Select(BoardRound.Next(Entries, Review, null) ?? Current);
        _progress.Text = BoardRound.Progress(Entries, Review);
        Detail.ShowSaved($"Round {Review.Round!.Number} started; saved to {_reviewName} at {DateTime.Now:HH:mm:ss}");
    }

    private void BuildRoundDialog()
    {
        var content = new VBoxContainer { CustomMinimumSize = new Vector2(460f, 0f) };
        content.AddThemeConstantOverride("separation", 10);
        content.AddChild(BoardText.Make("Every sound you've judged will read \"re-hear\" until you judge it again. "
                                        + "Your verdicts stay, and Keep on a sound you don't change only records that you heard it.",
                                        14, _style.Text, wrap: true));
        _roundLabel = new LineEdit { PlaceholderText = "A short name for the round, e.g. after the playtest" };
        content.AddChild(_roundLabel);
        _roundDialog = new ConfirmationDialog();
        _roundDialog.AddChild(content);
        _roundDialog.RegisterTextEnter(_roundLabel);
        _roundDialog.Confirmed += () =>
        {
            StartRound(_roundLabel.Text);
            List.GrabFocus();
        };
        _roundDialog.Canceled += () => List.GrabFocus();
        AddChild(_roundDialog);
    }

    /// <summary>A button under the list: like the detail's, it never takes focus, so the keys keep working.</summary>
    private Button ListButton(string text, string tooltip, Action pressed)
    {
        var button = new Button { Text = text, TooltipText = tooltip, FocusMode = FocusModeEnum.None };
        button.Pressed += () =>
        {
            List.GrabFocus();
            pressed();
        };
        return button;
    }

    /// <summary>A candidate or level changed after a verdict: the saved verdict follows it.</summary>
    private void FollowVerdict()
    {
        if (!Review.Entries.TryGetValue(Current.Id, out ReviewEntry? entry))
        {
            return;
        }
        (int candidate, float offset) = Candidates.PreviewOf(Current);
        entry.Candidate = candidate > 0 ? Candidates.For(Current)[candidate].Name : null;
        entry.File = Candidates.FileOf(Current, candidate);
        entry.LevelDb = offset;
        entry.At = Stamp();
        entry.Applied = null;
        SaveReview();
    }

    public void CommitNote(string note)
    {
        if (Review.Entries.TryGetValue(Current.Id, out ReviewEntry? entry) && entry.Note != note)
        {
            entry.Note = note;
            entry.At = Stamp();
            entry.Applied = null;
            SaveReview();
        }
    }

    private void SaveReview()
    {
        ReviewFile.Save(ReviewPath, Review);
        List.Refresh(Current, Review);
        ShowVerdict();
        _progress.Text = BoardRound.Progress(Entries, Review);
        Detail.ShowSaved($"Saved to {_reviewName} at {DateTime.Now:HH:mm:ss}");
    }

    private void ShowPreview()
    {
        IReadOnlyList<BoardCandidate> candidates = Candidates.For(Current);
        (int candidate, float offset) = Candidates.PreviewOf(Current);
        string sound = Current.Silent && candidates.Count == 1 ? "nothing plays"
            : $"{candidates[candidate].Name}  ({candidate + 1}/{candidates.Count})";
        Detail.ShowPreview(sound, $"{offset:+0;-0;0} dB from shipped");
    }

    private void ShowVerdict()
    {
        if (!Review.Entries.TryGetValue(Current.Id, out ReviewEntry? verdict))
        {
            Detail.ShowVerdict(Current.Silent ? "No verdict yet. Nothing plays here today." : "No verdict yet.", "");
            return;
        }
        string what = verdict.Verdict switch
        {
            Verdicts.Keep => "Keep",
            Verdicts.SilentOk => "Silent is right",
            Verdicts.NeedsSound => "Needs a sound",
            _ => "Replace",
        };
        if (verdict.Candidate is not null)
        {
            what += $", with {verdict.Candidate}";
        }
        if (verdict.LevelDb != 0f)
        {
            what += $", at {verdict.LevelDb:+0;-0;0} dB";
        }
        what += verdict.Applied is null ? "." : $". Applied: {verdict.Applied}";
        // From an earlier round it shows as what was said then; Keep or Replace makes it this round's.
        string whose = BoardRound.ToReHear(Current, Review) ? $"Last round ({verdict.At[..Math.Min(10, verdict.At.Length)]}): " : "Your verdict: ";
        Detail.ShowVerdict(whose + what, verdict.Note);
    }

    /// <summary>Whether the entry's shipped file is marked Confirm in the sources file.</summary>
    public bool NeedsConfirm(BoardEntry entry)
    {
        IReadOnlyList<string> paths = Candidates.For(entry)[0].Paths;
        return paths.Count > 0 && _licence.StatusFor(paths[0].GetFile()) == LicenceStatus.Confirm;
    }

    private string LicenceText(string file)
    {
        if (file.Length == 0 || _config.SourcesPath.Length == 0)
        {
            return "";
        }
        string sources = _config.SourcesPath.GetFile();
        return _licence.StatusFor(file) switch
        {
            LicenceStatus.Clean => $"Licence: on record in {sources}.",
            LicenceStatus.Confirm => $"Licence: marked Confirm in {sources} (resolve before release).",
            _ => $"Licence: no row in {sources}.",
        };
    }

    private Vector2[] EnvelopeOf(string path)
    {
        if (!_envelopes.TryGetValue(path, out Vector2[]? envelope))
        {
            envelope = Waveform.Envelope(path, WaveColumns);
            _envelopes[path] = envelope;
        }
        return envelope;
    }

    private string SelectedContext() =>
        Host.Contexts.Count == 0 ? "" : Host.Contexts[Math.Clamp(_context.Selected, 0, Host.Contexts.Count - 1)];

    private SoundBoardHost? FindHost()
    {
        foreach (Node child in GetChildren())
        {
            if (child is SoundBoardHost host)
            {
                return host;
            }
        }
        return null;
    }

    private void SaveShot(string path)
    {
        Image image = GetViewport().GetTexture().GetImage();
        string abs = ProjectSettings.GlobalizePath(path);
        Error error = image.SavePng(abs);
        GD.Print(error == Error.Ok ? $"[shot] saved {abs} {image.GetWidth()}x{image.GetHeight()}" : $"[shot] FAILED {abs}: {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return i;
            }
        }
        return -1;
    }

    private static string Stamp() => DateTime.Now.ToString("s", CultureInfo.InvariantCulture);

    private static bool HasArg(string wanted) => Array.IndexOf(OS.GetCmdlineUserArgs(), wanted) >= 0;
}
