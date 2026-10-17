using System;
using System.Collections.Generic;
using Godot;

namespace Soverance.SoundBoard;

/// <summary>
/// What a game gives the board: its sounds and how to play them the way the game does. A board scene is a
/// <see cref="BoardScreen"/> with one child that derives from this. Build the game's audio in <c>_Ready</c> (children
/// are ready before the screen). Previews apply in memory only: nothing the host does here may write a game file.
/// </summary>
public abstract partial class SoundBoardHost : Node
{
    public virtual BoardConfig Config { get; } = new();

    /// <summary>Every entry, in list order. Read once, after <c>_Ready</c>.</summary>
    public abstract IReadOnlyList<BoardEntry> Entries { get; }

    /// <summary>The moments IN CONTEXT can play over (a battle's ambience, a level's music). Empty hides it.</summary>
    public virtual IReadOnlyList<string> Contexts => Array.Empty<string>();

    /// <summary>The context to select when <paramref name="entry"/> is selected, given the one selected now (a UI
    /// sound may want the menu's). Returns <paramref name="current"/> to leave the choice alone.</summary>
    public virtual string ContextFor(BoardEntry entry, string current) => current;

    /// <summary>The visual look; override to match a game's palette.</summary>
    public virtual BoardStyle Style => BoardStyle.Default;

    /// <summary>The takes the game ships for this entry (empty when silent): the first candidate, and the
    /// waveform's source.</summary>
    public abstract IReadOnlyList<AudioStream> ShippedTakes(BoardEntry entry);

    /// <summary>From now on, play <paramref name="takes"/> for this entry at its shipped level plus
    /// <paramref name="offsetDb"/>. <paramref name="shipped"/> says the takes are the game's own (restore anything
    /// the game sets for them, such as a pitch).</summary>
    public abstract void Preview(BoardEntry entry, IReadOnlyList<AudioStream> takes, bool shipped, float offsetDb);

    /// <summary>Plays the entry once as the game would. Release whatever the last play held (a loop) first.</summary>
    public abstract void Play(BoardEntry entry, PlayMode mode);

    /// <summary>Silence everything, contexts included.</summary>
    public abstract void StopAll();

    public virtual void StartContext(string context)
    {
    }

    public virtual void StopContext()
    {
    }

    /// <summary>The "Now:" line: file, level and the rules it plays by.</summary>
    public virtual string Describe(BoardEntry entry, float offsetDb) => "";

    /// <summary>The header's right side (the context running, volumes), refreshed four times a second.</summary>
    public virtual string Status() => "";

    /// <summary>How many times the game has played this entry, for <c>--board-test</c>; -1 when it isn't counted.</summary>
    public virtual int Plays(BoardEntry entry) => -1;

    /// <summary>The game's own checks for <c>--board-test</c> (coverage: every sound the game can make is an entry).</summary>
    public virtual void SelfTest(BoardTestReport report)
    {
    }
}
