using System;
using Godot;

namespace Soverance.SoundBoard;

/// <summary>The board's right side: what the selected sound is, the buttons that play and judge it, and the note.
/// Every button shows its key; every key is optional.</summary>
public partial class BoardDetail : VBoxContainer
{
    public event Action? SoloPressed;
    public event Action? ContextPressed;
    public event Action? BurstPressed;
    public event Action? StopPressed;
    public event Action<int>? CandidateStepped;

    /// <summary>USE A FILE…: hear a file from disk as this sound.</summary>
    public event Action? FileRequested;
    public event Action<float>? LevelStepped;

    /// <summary>true = K (Keep / Silent is right), false = R (Replace / Needs a sound).</summary>
    public event Action<bool>? VerdictPressed;

    public event Action<string>? NoteCommitted;

    /// <summary>The note is done with: Enter in it, or any button clicked. The board gives the list focus back, so the
    /// arrows and the keys work again.</summary>
    public event Action? FocusReturned;

    public BoardStyle Style { get; set; } = BoardStyle.Default;

    private Label _title = null!;
    private Label _id = null!;
    private Label _when = null!;
    private Label _now = null!;
    private Label _licence = null!;
    private Label _candidate = null!;
    private Label _level = null!;
    private Label _verdict = null!;
    private Label _saved = null!;
    private WaveformView _wave = null!;
    private Button _context = null!;
    private Button _burst = null!;
    private Button _prev = null!;
    private Button _next = null!;
    private Button _file = null!;
    private Button _keep = null!;
    private Button _replace = null!;

    public LineEdit NoteField { get; private set; } = null!;

    /// <summary>The Keep / Silent is right button (test access).</summary>
    public Button KeepButton => _keep;

    public string Note => NoteField.Text;

    public string SavedText => _saved.Text;

    public string VerdictText => _verdict.Text;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 10);
        _title = BoardText.Make("", 20, Style.Title);
        _id = BoardText.Make("", 12, Style.TextDim);
        _when = BoardText.Make("", 15, Style.Text, wrap: true);
        _now = BoardText.Make("", 13, Style.TextDim, wrap: true);
        _wave = new WaveformView { CustomMinimumSize = new Vector2(0f, 96f), Style = Style };
        AddChild(_title);
        AddChild(_id);
        AddChild(_when);
        AddChild(_now);
        AddChild(_wave);

        AddChild(Row(
            MakeButton("SOLO  [Space]", "Play it once, with everything else stopped.", () => SoloPressed?.Invoke()),
            _context = MakeButton("IN CONTEXT  [C]", "Play it over the moment chosen at the top.", () => ContextPressed?.Invoke()),
            _burst = MakeButton("BURST  [B]", "Ten in 200 ms: what the voice limits do in a crowd.", () => BurstPressed?.Invoke()),
            MakeButton("STOP ALL  [Esc]", "Silence everything, the context too.", () => StopPressed?.Invoke())));

        _candidate = BoardText.Make("", 14, Style.Accent);
        AddChild(Row(
            BoardText.Make("SOUND", 11, Style.TextDim),
            _prev = MakeButton("< PREV  [", "The previous candidate.", () => CandidateStepped?.Invoke(-1)),
            _candidate,
            _next = MakeButton("NEXT  ] >", "The next candidate.", () => CandidateStepped?.Invoke(1)),
            _file = MakeButton("USE A FILE…  [F]",
                               "Pick a WAV, OGG or MP3 from disk and hear it here as this sound. Keep records it for the agent.",
                               () => FileRequested?.Invoke())));

        _level = BoardText.Make("", 14, Style.Accent);
        AddChild(Row(
            BoardText.Make("LEVEL", 11, Style.TextDim),
            MakeButton("-1 dB  [-]", "1 dB quieter.", () => LevelStepped?.Invoke(-1f)),
            _level,
            MakeButton("+1 dB  [=]", "1 dB louder.", () => LevelStepped?.Invoke(1f))));

        _keep = MakeButton("", "", () => VerdictPressed?.Invoke(true));
        _replace = MakeButton("", "", () => VerdictPressed?.Invoke(false));
        AddChild(Row(_keep, _replace));

        NoteField = new LineEdit
        {
            PlaceholderText = "Note for the agent (optional), saved with your verdict",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        NoteField.TextSubmitted += text =>
        {
            NoteCommitted?.Invoke(text);
            FocusReturned?.Invoke();
        };
        NoteField.FocusExited += () => NoteCommitted?.Invoke(NoteField.Text);
        AddChild(NoteField);

        _verdict = BoardText.Make("", 14, Style.Good, wrap: true);
        _licence = BoardText.Make("", 13, Style.TextDim, wrap: true);
        _saved = BoardText.Make("", 12, Style.TextDim, wrap: true);
        AddChild(_verdict);
        AddChild(_licence);
        AddChild(_saved);
    }

    public void ShowEntry(BoardEntry entry, string now, string licence, Vector2[] envelope, bool hasContexts,
                          bool hasCandidates, bool takesFile)
    {
        _title.Text = entry.Label.ToUpperInvariant();
        _id.Text = entry.Id;
        _when.Text = "Plays when: " + entry.When;
        _now.Text = now;
        _licence.Text = licence;
        _wave.Envelope = envelope;
        _context.Disabled = !hasContexts;
        _burst.Disabled = !entry.CanBurst;
        _prev.Disabled = _next.Disabled = !hasCandidates;
        _file.Disabled = !takesFile;
        _keep.Text = entry.Silent ? "SILENT IS RIGHT  [K]" : "KEEP  [K]";
        _keep.TooltipText = entry.Silent ? "Nothing should play here." : "This sound is right, at the level and candidate shown.";
        _replace.Text = entry.Silent ? "NEEDS A SOUND  [R]" : "REPLACE  [R]";
        _replace.TooltipText = entry.Silent
            ? "Ask the agent to find a sound for this (say what kind in the note)."
            : "Ask the agent to change it: the candidate shown, or something else (say what in the note).";
    }

    public void ShowPreview(string candidate, string level)
    {
        _candidate.Text = candidate;
        _level.Text = level;
    }

    public void ShowVerdict(string text, string note)
    {
        _verdict.Text = text;
        if (!NoteField.HasFocus())
        {
            NoteField.Text = note;
        }
    }

    public void ShowSaved(string text) => _saved.Text = text;

    private static HBoxContainer Row(params Control[] children)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        foreach (Control child in children)
        {
            row.AddChild(child);
        }
        return row;
    }

    /// <summary>Buttons never take focus, and a click takes it back from the note, so the keys keep working.</summary>
    private Button MakeButton(string text, string tooltip, Action pressed)
    {
        var button = new Button { Text = text, TooltipText = tooltip, FocusMode = FocusModeEnum.None };
        button.Pressed += () =>
        {
            FocusReturned?.Invoke();
            pressed();
        };
        return button;
    }
}
