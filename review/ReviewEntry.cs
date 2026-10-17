namespace Soverance.SoundBoard;

/// <summary>A reviewer's verdict on one sound, as saved in <c>review.json</c>.</summary>
public sealed class ReviewEntry
{
    /// <summary>One of the <see cref="Verdicts"/>.</summary>
    public string Verdict { get; set; } = "";

    /// <summary>The audition candidate chosen, or null for the shipped sound.</summary>
    public string? Candidate { get; set; }

    /// <summary>When the candidate is a file picked from disk (USE A FILE…): its full path. The agent cuts,
    /// imports and locks it in.</summary>
    public string? File { get; set; }

    /// <summary>The chosen level, relative to the shipped level, dB.</summary>
    public float LevelDb { get; set; }

    public string Note { get; set; } = "";

    /// <summary>When it was given (local time, ISO 8601 to the second).</summary>
    public string At { get; set; } = "";

    /// <summary>What the agent did about it and when; null until applied. A new verdict clears it, but a Keep that
    /// changes nothing only re-stamps <see cref="At"/>.</summary>
    public string? Applied { get; set; }
}
