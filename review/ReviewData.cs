using System;
using System.Collections.Generic;

namespace Soverance.SoundBoard;

/// <summary>The whole of <c>review.json</c>: verdicts keyed by board entry id, sorted so diffs stay small.</summary>
public sealed class ReviewData
{
    public int Version { get; set; } = 1;

    /// <summary>Null until round 2 starts; a file without it saves without it.</summary>
    public ReviewRound? Round { get; set; }

    public SortedDictionary<string, ReviewEntry> Entries { get; set; } = new(StringComparer.Ordinal);
}
