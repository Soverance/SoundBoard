using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;

namespace Soverance.SoundBoard;

/// <summary>
/// Each entry's candidates (shipped first, then its audition folder's, then files picked from disk) and the preview
/// chosen for it: which candidate, at what level. A preview reaches the game through
/// <see cref="SoundBoardHost.Preview"/>, in memory only, for the session.
/// </summary>
public sealed partial class Candidates
{
    /// <summary>How a file picked from disk is named among the candidates.</summary>
    public const string FilePrefix = "file: ";

    public const string ShippedName = "shipped";

    private static readonly string[] AudioExtensions = [".wav", ".ogg", ".mp3"];

    private readonly SoundBoardHost _host;
    private readonly string _root;
    private readonly Dictionary<string, List<BoardCandidate>> _lists = [];
    private readonly Dictionary<string, (int Candidate, float OffsetDb)> _previews = [];

    public Candidates(SoundBoardHost host, string auditionRoot)
    {
        _host = host;
        _root = auditionRoot;
    }

    /// <summary>"shipped" first, then the audition folder's candidates in name order, then any files added.</summary>
    public IReadOnlyList<BoardCandidate> For(BoardEntry entry) => ListFor(entry);

    private List<BoardCandidate> ListFor(BoardEntry entry)
    {
        if (!_lists.TryGetValue(entry.Id, out List<BoardCandidate>? list))
        {
            IReadOnlyList<AudioStream> shipped = _host.ShippedTakes(entry);
            var paths = new List<string>();
            foreach (AudioStream take in shipped)
            {
                paths.Add(Waveform.SourceFile(take));
            }
            list = [new BoardCandidate(ShippedName, shipped, paths)];
            if (entry.AuditionKey is { Length: > 0 } key)
            {
                list.AddRange(LoadAudition(_root, key));
            }
            _lists[entry.Id] = list;
        }
        return list;
    }

    public (int Candidate, float OffsetDb) PreviewOf(BoardEntry entry) => _previews.GetValueOrDefault(entry.Id, (0, 0f));

    public bool HasPreview(BoardEntry entry) => _previews.ContainsKey(entry.Id);

    public void SetPreview(BoardEntry entry, int candidate, float offsetDb)
    {
        List<BoardCandidate> list = ListFor(entry);
        candidate = Math.Clamp(candidate, 0, list.Count - 1);
        _previews[entry.Id] = (candidate, offsetDb);
        _host.Preview(entry, list[candidate].Takes, candidate == 0, offsetDb);
    }

    /// <summary>The full path of a file candidate, or null for the shipped sound or an audition candidate.</summary>
    public string? FileOf(BoardEntry entry, int candidate)
    {
        List<BoardCandidate> list = ListFor(entry);
        return candidate > 0 && candidate < list.Count ? list[candidate].File : null;
    }

    /// <summary>Adds a WAV, OGG or MP3 from anywhere on disk as one of the entry's candidates (loaded now, not
    /// imported) and returns its index; the same file again returns the same candidate. -1 when the entry can't take
    /// a file or the file won't load, with <paramref name="error"/> saying why.</summary>
    public int AddFile(BoardEntry entry, string path, out string? error)
    {
        error = null;
        if (!entry.TakesFile || entry.Silent)
        {
            error = "This sound doesn't take a file.";
            return -1;
        }
        List<BoardCandidate> list = ListFor(entry);
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i].File == path)
            {
                return i;
            }
        }
        if (LoadStream(path) is not { } stream)
        {
            error = $"Couldn't load {System.IO.Path.GetFileName(path)}: the board takes an existing WAV, OGG or MP3 file.";
            return -1;
        }
        list.Add(new BoardCandidate(FilePrefix + System.IO.Path.GetFileName(path), [stream], [path], path));
        return list.Count - 1;
    }

    /// <summary>
    /// The candidates under <c>&lt;root&gt;/&lt;key&gt;/</c>, in name order. A folder is one candidate whose sound files are
    /// its takes; loose files that share a name but for a trailing <c>_N</c> are the takes of one candidate
    /// (<c>walk_1.wav</c> and <c>walk_2.wav</c> are <c>walk</c>), and a lone file is a candidate under its whole name.
    /// </summary>
    public static List<BoardCandidate> LoadAudition(string root, string key)
    {
        var found = new SortedDictionary<string, (List<AudioStream> Takes, List<string> Paths)>(StringComparer.Ordinal);
        string dir = root.TrimEnd('/') + "/" + key;
        if (!DirAccess.DirExistsAbsolute(dir))
        {
            return [];
        }
        foreach (string folder in Sorted(DirAccess.GetDirectoriesAt(dir)))
        {
            foreach (string file in Sorted(DirAccess.GetFilesAt($"{dir}/{folder}")))
            {
                Add(found, folder, $"{dir}/{folder}/{file}");
            }
        }
        var loose = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string file in Sorted(DirAccess.GetFilesAt(dir)))
        {
            string stem = TakeSuffix().Replace(System.IO.Path.GetFileNameWithoutExtension(file), "");
            if (!loose.TryGetValue(stem, out List<string>? files))
            {
                loose[stem] = files = [];
            }
            files.Add(file);
        }
        foreach ((string stem, List<string> files) in loose)
        {
            // A lone file keeps its whole name (orch_victory_01 is one sound, not a take of orch_victory).
            foreach (string file in files)
            {
                Add(found, files.Count > 1 ? stem : System.IO.Path.GetFileNameWithoutExtension(file), $"{dir}/{file}");
            }
        }
        var result = new List<BoardCandidate>();
        foreach ((string name, (List<AudioStream> takes, List<string> paths)) in found)
        {
            if (takes.Count > 0)
            {
                result.Add(new BoardCandidate(name, takes, paths));
            }
        }
        return result;
    }

    private static void Add(SortedDictionary<string, (List<AudioStream>, List<string>)> found, string name, string path)
    {
        if (!IsAudio(path) || LoadStream(path) is not { } stream)
        {
            return;
        }
        if (!found.TryGetValue(name, out (List<AudioStream> Takes, List<string> Paths) slot))
        {
            slot = ([], []);
            found[name] = slot;
        }
        slot.Takes.Add(stream);
        slot.Paths.Add(path);
    }

    /// <summary>An imported resource when there is one (so the game's import settings apply), else the file itself,
    /// so a candidate cut a moment ago plays before the editor imports it.</summary>
    public static AudioStream? LoadStream(string path)
    {
        if (path.StartsWith("res://", StringComparison.Ordinal) && ResourceLoader.Exists(path))
        {
            return GD.Load<AudioStream>(path);
        }
        string abs = ProjectSettings.GlobalizePath(path);
        if (!System.IO.File.Exists(abs))
        {
            return null;
        }
        return System.IO.Path.GetExtension(abs).ToLowerInvariant() switch
        {
            ".wav" => AudioStreamWav.LoadFromFile(abs),
            ".ogg" => AudioStreamOggVorbis.LoadFromFile(abs),
            ".mp3" => AudioStreamMP3.LoadFromFile(abs),
            _ => null,
        };
    }

    private static bool IsAudio(string path)
    {
        foreach (string extension in AudioExtensions)
        {
            if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string[] Sorted(string[] names)
    {
        Array.Sort(names, StringComparer.Ordinal);
        return names;
    }

    [GeneratedRegex(@"_\d+$")]
    private static partial Regex TakeSuffix();
}
