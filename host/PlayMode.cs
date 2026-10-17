namespace Soverance.SoundBoard;

/// <summary>How the board asks the host to play an entry.</summary>
public enum PlayMode
{
    /// <summary>Once, alone: the board has already stopped any context.</summary>
    Solo,

    /// <summary>Once, over the context the board has started.</summary>
    InContext,

    /// <summary>One shot of a burst: play it once and hold nothing (the board fires ten of these in 200 ms).</summary>
    BurstShot,
}
