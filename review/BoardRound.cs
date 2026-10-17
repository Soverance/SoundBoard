using System.Collections.Generic;

namespace Soverance.SoundBoard;

/// <summary>
/// Review rounds: a pass over the whole board, so the reviewer can re-hear everything after a playtest or a new
/// library and see what has been done this time. Round 1 (no round in the file) is the first pass over every sound.
/// From round 2 an entry that <see cref="BoardEntry.SitsOutRounds"/> can still be judged, but never counts or reads
/// "re-hear".
/// </summary>
public static class BoardRound
{
    public static int Number(ReviewData review) => review.Round?.Number ?? 1;

    /// <summary>Whether the entry counts toward the round.</summary>
    public static bool InScope(BoardEntry entry, ReviewData review) => review.Round is null || !entry.SitsOutRounds;

    /// <summary>A verdict given since the round started. Stamps are ISO 8601 to the second, so they sort as text.</summary>
    public static bool Heard(BoardEntry entry, ReviewData review) =>
        review.Entries.TryGetValue(entry.Id, out ReviewEntry? verdict)
        && (review.Round is null || string.CompareOrdinal(verdict.At, review.Round.Started) >= 0);

    /// <summary>In the round with a verdict from an earlier one: the list reads "re-hear".</summary>
    public static bool ToReHear(BoardEntry entry, ReviewData review) =>
        review.Round is not null && InScope(entry, review) && review.Entries.ContainsKey(entry.Id) && !Heard(entry, review);

    /// <summary>An applied Replace or Needs a sound that names no candidate or file: the agent has cut candidates for
    /// it and the current sound still ships, so the reviewer's pick is next (the list reads "pick").</summary>
    public static bool ToPick(ReviewEntry verdict) =>
        verdict.Applied is not null && verdict.Verdict is Verdicts.Replace or Verdicts.NeedsSound
        && verdict.Candidate is null && verdict.File is null;

    /// <summary>The next entry in the round not heard in it, after <paramref name="after"/> in list order and wrapping
    /// round (from the top when null); null when the round is done.</summary>
    public static BoardEntry? Next(IReadOnlyList<BoardEntry> entries, ReviewData review, BoardEntry? after)
    {
        int from = 0;
        for (int i = 0; after is not null && i < entries.Count; i++)
        {
            if (ReferenceEquals(entries[i], after))
            {
                from = i + 1;
            }
        }
        for (int step = 0; step < entries.Count; step++)
        {
            BoardEntry entry = entries[(from + step) % entries.Count];
            if (InScope(entry, review) && !Heard(entry, review))
            {
                return entry;
            }
        }
        return null;
    }

    /// <summary>"Round 2 · after the playtest · 37 / 186 re-heard · 5 replace": the round's entries heard in it, and how
    /// many of those ask for a change not yet applied; then " · 28 to pick" while any have candidates waiting.</summary>
    public static string Progress(IReadOnlyList<BoardEntry> entries, ReviewData review)
    {
        int scope = 0;
        int heard = 0;
        int replace = 0;
        int pick = 0;
        foreach (BoardEntry entry in entries)
        {
            if (!InScope(entry, review))
            {
                continue;
            }
            scope++;
            if (!Heard(entry, review))
            {
                continue;
            }
            heard++;
            ReviewEntry verdict = review.Entries[entry.Id];
            replace += verdict.Applied is null && verdict.Verdict is Verdicts.Replace or Verdicts.NeedsSound ? 1 : 0;
            pick += ToPick(verdict) ? 1 : 0;
        }
        string label = review.Round is { Label.Length: > 0 } round ? $" · {round.Label}" : "";
        string done = review.Round is null ? "heard" : "re-heard";
        string picks = pick > 0 ? $" · {pick} to pick" : "";
        return $"Round {Number(review)}{label} · {heard} / {scope} {done} · {replace} replace{picks}";
    }

    /// <summary>Starts the next round: every verdict before now is to re-hear.</summary>
    public static void Start(ReviewData review, string label, string now) =>
        review.Round = new ReviewRound { Number = Number(review) + 1, Label = label.Trim(), Started = now };
}
