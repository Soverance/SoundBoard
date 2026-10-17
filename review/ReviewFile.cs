using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Soverance.SoundBoard;

/// <summary>Loads and saves <c>review.json</c>, the board's verdicts for whoever applies them.</summary>
public static class ReviewFile
{
    // The file lives in git, so a save must only change the lines whose verdict changed. The default
    // encoder would rewrite every "->" or "+2" in old notes into JSON hex escapes; the relaxed one is
    // safe here because the file is never embedded in HTML.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The verdicts in the file; empty when it doesn't exist. A file that won't parse is kept beside it as
    /// <c>.bad</c> (the next save can't lose it) and <paramref name="error"/> says so.</summary>
    public static ReviewData Load(string absPath, out string? error)
    {
        error = null;
        if (!File.Exists(absPath))
        {
            return new ReviewData();
        }
        ReviewData? data;
        try
        {
            data = JsonSerializer.Deserialize<ReviewData>(File.ReadAllText(absPath), Options);
        }
        catch (JsonException e)
        {
            return Malformed(absPath, e.Message, out error);
        }
        if (data is null)
        {
            return new ReviewData();
        }
        // A hand edit can parse and still hold nulls; the board would trip over them.
        if (data.Entries is null)
        {
            return Malformed(absPath, "\"entries\" is null", out error);
        }
        if (data.Round is { Label: null } or { Started: null })
        {
            return Malformed(absPath, "\"round\" has no label or start", out error);
        }
        foreach (KeyValuePair<string, ReviewEntry> entry in data.Entries)
        {
            if (entry.Value is null)
            {
                return Malformed(absPath, $"\"{entry.Key}\" is null", out error);
            }
        }
        return data;
    }

    private static ReviewData Malformed(string absPath, string why, out string? error)
    {
        string bad = absPath + ".bad";
        File.Copy(absPath, bad, overwrite: true);
        error = $"{Path.GetFileName(absPath)} couldn't be read ({why}); it was kept as {Path.GetFileName(bad)}.";
        return new ReviewData();
    }

    /// <summary>Writes the whole file through a temp file and a replace, so a crash mid-write can't leave half a file.</summary>
    public static void Save(string absPath, ReviewData data)
    {
        string? dir = Path.GetDirectoryName(absPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string tmp = absPath + ".tmp";
        File.WriteAllText(tmp, ToJson(data));
        File.Move(tmp, absPath, overwrite: true);
    }

    /// <summary>The file's text: LF line ends and a final newline on every OS, so a save on Windows and one on Linux
    /// are the same bytes. (net8's indented writer uses Environment.NewLine; strings carry their own newlines escaped,
    /// so every raw CR is layout.)</summary>
    public static string ToJson(ReviewData data) =>
        JsonSerializer.Serialize(data, Options).Replace("\r\n", "\n") + "\n";
}
