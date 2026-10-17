namespace Soverance.SoundBoard;

/// <summary>The review pass under way, as saved in <c>review.json</c>. A verdict given since <see cref="Started"/>
/// is heard this round; one before it is to re-hear. No round in the file is round 1, begun before any verdict.</summary>
public sealed class ReviewRound
{
    public int Number { get; set; }

    /// <summary>The reviewer's name for it ("after the playtest"); may be empty.</summary>
    public string Label { get; set; } = "";

    /// <summary>When it started (local time, ISO 8601, as a verdict's <c>at</c>).</summary>
    public string Started { get; set; } = "";
}
