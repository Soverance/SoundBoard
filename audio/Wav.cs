using System;
using System.IO;
using System.Text;

namespace Soverance.SoundBoard;

/// <summary>
/// Minimal WAV I/O: the waveform reads files with it and the board test writes its fixtures. Walks RIFF chunks;
/// reads 16/24/32-bit PCM and 32-bit float (plain or WAVE_FORMAT_EXTENSIBLE, as sound libraries
/// ship them) into interleaved floats, writes a minimal 16-bit PCM file.
/// </summary>
public static class Wav
{
    private const int TagPcm = 1;
    private const int TagFloat = 3;
    private const int TagExtensible = 0xFFFE;

    /// <summary>Returns false for anything but PCM 16/24/32 and float 32.</summary>
    public static bool TryRead(string absPath, out float[] samples, out int channels, out int rate)
    {
        samples = [];
        channels = 0;
        rate = 0;

        byte[] b;
        try
        {
            b = File.ReadAllBytes(absPath);
        }
        catch (IOException)
        {
            return false;
        }
        if (b.Length < 44 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF" || Encoding.ASCII.GetString(b, 8, 4) != "WAVE")
        {
            return false;
        }

        int pos = 12;
        int fmt = -1;
        int data = -1;
        int dataLen = 0;
        while (pos < b.Length - 8)
        {
            string id = Encoding.ASCII.GetString(b, pos, 4);
            int len = BitConverter.ToInt32(b, pos + 4);
            if (id == "fmt ")
            {
                fmt = pos + 8;
            }
            else if (id == "data")
            {
                data = pos + 8;
                dataLen = len;
                break;
            }
            pos += 8 + len + (len & 1);
        }
        if (fmt < 0 || data < 0)
        {
            return false;
        }

        int formatTag = BitConverter.ToUInt16(b, fmt);
        int fileChannels = BitConverter.ToInt16(b, fmt + 2);
        int fileRate = BitConverter.ToInt32(b, fmt + 4);
        int bits = BitConverter.ToInt16(b, fmt + 14);
        if (formatTag == TagExtensible)
        {
            // The real format is the first two bytes of the SubFormat GUID, after cbSize (22 bytes of extension).
            formatTag = BitConverter.ToUInt16(b, fmt + 24);
        }
        bool pcm = formatTag == TagPcm && bits is 16 or 24 or 32;
        bool isFloat = formatTag == TagFloat && bits == 32;
        if ((!pcm && !isFloat) || fileChannels <= 0)
        {
            return false;
        }

        int bytesPerSample = bits / 8;
        int frameBytes = bytesPerSample * fileChannels;
        int availBytes = Math.Min(dataLen, b.Length - data);
        int frames = availBytes / frameBytes;

        var result = new float[frames * fileChannels];
        int o = data;
        for (int i = 0; i < result.Length; i++)
        {
            if (isFloat)
            {
                result[i] = BitConverter.ToSingle(b, o);
            }
            else if (bits == 16)
            {
                result[i] = (short)(b[o] | (b[o + 1] << 8)) / 32768f;
            }
            else if (bits == 24)
            {
                int v = b[o] | (b[o + 1] << 8) | (b[o + 2] << 16);
                if ((v & 0x800000) != 0)
                {
                    v -= 0x1000000;
                }
                result[i] = v / 8388608f;
            }
            else
            {
                result[i] = BitConverter.ToInt32(b, o) / 2147483648f;
            }
            o += bytesPerSample;
        }

        samples = result;
        channels = fileChannels;
        rate = fileRate;
        return true;
    }

    /// <summary>Writes a minimal RIFF/WAVE: fmt (tag 1, 16-bit) + data. Clamps to [-1, 1] first.</summary>
    public static void Write16(string absPath, float[] samples, int channels, int rate)
    {
        int frameBytes = 2 * channels;
        int dataLen = samples.Length * 2;

        string? dir = Path.GetDirectoryName(absPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = new FileStream(absPath, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(stream);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataLen);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);
        w.Write((short)channels);
        w.Write(rate);
        w.Write(rate * frameBytes);
        w.Write((short)frameBytes);
        w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataLen);
        foreach (float s in samples)
        {
            float c = Math.Clamp(s, -1f, 1f);
            short v = (short)Math.Round(c * (c < 0 ? 32768f : 32767f), MidpointRounding.AwayFromZero);
            w.Write(v);
        }
    }
}
