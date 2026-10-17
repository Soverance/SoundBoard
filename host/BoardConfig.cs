namespace Soverance.SoundBoard;

/// <summary>Where a game keeps the files the board reads and writes. Every path may be <c>res://</c>, <c>user://</c> or
/// absolute.</summary>
public sealed class BoardConfig
{
    /// <summary>The verdicts. Commit it, so every verdict has a diff.</summary>
    public string ReviewPath { get; init; } = "res://audio/review.json";

    /// <summary>Holds <c>&lt;audition key&gt;/</c> folders of candidates.</summary>
    public string AuditionRoot { get; init; } = "res://audio/audition";

    /// <summary>A markdown file whose table rows name each shipped file in their first cell; a row containing
    /// "Confirm" has rights to resolve. Empty to skip the licence line.</summary>
    public string SourcesPath { get; init; } = "res://audio/SOURCES.md";

    /// <summary>Where USE A FILE… opens the first time (a sound library); empty for the OS default.</summary>
    public string LibraryRoot { get; init; } = "";

    /// <summary>The board's heading.</summary>
    public string Title { get; init; } = "SOUND BOARD";

    /// <summary>The line of help across the top.</summary>
    public string Help { get; init; } = "Pick a sound, play it, then Keep or Replace. Your choices save as you go.";
}
