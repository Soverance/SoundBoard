namespace Soverance.SoundBoard;

/// <summary>The four verdicts a reviewer can give, as written to <c>review.json</c>.</summary>
public static class Verdicts
{
    /// <summary>The sound is right, at the candidate and level shown.</summary>
    public const string Keep = "keep";

    /// <summary>Change it: to the candidate shown, or, with none, to something new.</summary>
    public const string Replace = "replace";

    /// <summary>An entry that plays nothing today should play something.</summary>
    public const string NeedsSound = "needsSound";

    /// <summary>An entry that plays nothing today is right to play nothing.</summary>
    public const string SilentOk = "silentOk";
}
