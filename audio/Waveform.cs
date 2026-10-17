using System;
using Godot;

namespace Soverance.SoundBoard;

/// <summary>A sound's source file and its peak envelope, for the board's waveform.</summary>
public static class Waveform
{
    /// <summary>The WAV a stream plays from (a randomizer's first variant) as a resource path, or "" if it isn't a
    /// WAV loaded from a file.</summary>
    public static string SourceFile(AudioStream? stream)
    {
        if (stream is AudioStreamRandomizer { StreamsCount: > 0 } variants)
        {
            stream = variants.GetStream(0);
        }
        string path = stream?.ResourcePath ?? "";
        return path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? path : "";
    }

    /// <summary>For each of <paramref name="columns"/> equal slices of the file, its lowest (x) and highest (y) sample
    /// across channels; empty when the file can't be read (not a WAV, or an encoding the reader doesn't know).</summary>
    public static Vector2[] Envelope(string path, int columns)
    {
        if (path.Length == 0 || columns <= 0
            || !Wav.TryRead(ProjectSettings.GlobalizePath(path), out float[] samples, out int channels, out _))
        {
            return [];
        }
        int frames = samples.Length / Math.Max(1, channels);
        if (frames == 0)
        {
            return [];
        }
        var envelope = new Vector2[columns];
        for (int c = 0; c < columns; c++)
        {
            int from = (int)((long)frames * c / columns);
            int to = Math.Max(from + 1, (int)((long)frames * (c + 1) / columns));
            float lo = 0f;
            float hi = 0f;
            for (int f = from; f < to && f < frames; f++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    float s = samples[f * channels + ch];
                    lo = Math.Min(lo, s);
                    hi = Math.Max(hi, s);
                }
            }
            envelope[c] = new Vector2(lo, hi);
        }
        return envelope;
    }
}
