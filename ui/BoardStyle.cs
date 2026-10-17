using Godot;

namespace Soverance.SoundBoard;

/// <summary>The board's colours, with a neutral dark default. A host may return its own to match a game's palette;
/// fonts stay the project theme's.</summary>
public sealed record BoardStyle
{
    public static readonly BoardStyle Default = new();

    public Color Background { get; init; } = new(0.055f, 0.059f, 0.075f);
    public Color Title { get; init; } = new(0.93f, 0.94f, 0.97f);
    public Color Text { get; init; } = new(0.80f, 0.82f, 0.86f);
    public Color TextDim { get; init; } = new(0.55f, 0.58f, 0.64f);
    public Color TextFaint { get; init; } = new(0.38f, 0.40f, 0.45f);

    /// <summary>Progress, the candidate and level readouts, the waveform.</summary>
    public Color Accent { get; init; } = new(0.55f, 0.80f, 0.95f);

    /// <summary>Kept, done, silent ok, and the verdict line.</summary>
    public Color Good { get; init; } = new(0.55f, 0.85f, 0.55f);

    /// <summary>Replace, needs sound, and a licence to confirm.</summary>
    public Color Warn { get; init; } = new(0.95f, 0.68f, 0.35f);

    /// <summary>An entry that plays nothing and has no verdict yet.</summary>
    public Color Silent { get; init; } = new(0.92f, 0.42f, 0.42f);

    public Color WaveBack { get; init; } = new(0.09f, 0.10f, 0.12f);
    public Color WaveLine { get; init; } = new(0.20f, 0.22f, 0.26f);
}
