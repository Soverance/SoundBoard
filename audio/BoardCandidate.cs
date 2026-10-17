using System.Collections.Generic;
using Godot;

namespace Soverance.SoundBoard;

/// <summary>
/// One sound an entry could play: the shipped one, an audition candidate, or a file from disk. <see cref="Takes"/> are
/// its variants (one per play); <see cref="Paths"/> are where they came from, for the waveform. <see cref="File"/> is the
/// full path of a file picked from disk, null otherwise.
/// </summary>
public sealed record BoardCandidate(string Name, IReadOnlyList<AudioStream> Takes, IReadOnlyList<string> Paths, string? File = null);
