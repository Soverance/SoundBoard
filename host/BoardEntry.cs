namespace Soverance.SoundBoard;

/// <summary>
/// One sound-worthy moment on the board, as the host lists it. The board shows entries in the order given, under a
/// heading each time <see cref="Category"/> changes.
/// </summary>
public sealed class BoardEntry
{
    public BoardEntry(string id, string category, string label, string when)
    {
        Id = id;
        Category = category;
        Label = label;
        When = when;
    }

    /// <summary>Stable and unique: the key of its verdict in <c>review.json</c> (e.g. <c>cue:sword_hit</c>).</summary>
    public string Id { get; }

    public string Category { get; }

    public string Label { get; }

    /// <summary>"Plays when", in plain words: what the player just did or saw.</summary>
    public string When { get; }

    /// <summary>Its folder under the audition root, where its candidates wait; null when it takes none.</summary>
    public string? AuditionKey { get; init; }

    /// <summary>Nothing plays here today, by design or by omission: the verdicts read "Silent is right" and
    /// "Needs a sound".</summary>
    public bool Silent { get; init; }

    /// <summary>Whether USE A FILE… applies (a file from disk heard in its place).</summary>
    public bool TakesFile { get; init; } = true;

    /// <summary>Whether BURST applies (ten plays in 200 ms: what the voice limits do in a crowd).</summary>
    public bool CanBurst { get; init; } = true;

    /// <summary>From round 2 it can still be judged but never counts or reads "re-hear" (a game's music, say).</summary>
    public bool SitsOutRounds { get; init; }

    /// <summary>Anything the host wants back when it is asked to play this entry.</summary>
    public object? Tag { get; init; }

    public override string ToString() => Id;
}
