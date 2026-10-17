using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace Soverance.SoundBoard;

/// <summary>
/// Reads a markdown sources file (<see cref="BoardConfig.SourcesPath"/>): the table row whose first cell names a file
/// says where it came from, and a row containing "Confirm" has rights to resolve before release.
/// </summary>
public sealed class SourceLicence
{
    private readonly List<(string FirstCell, string Row)> _rows;

    private SourceLicence(List<(string FirstCell, string Row)> rows) => _rows = rows;

    /// <summary>The file's rows; none when the path is empty or the file is missing.</summary>
    public static SourceLicence Load(string path)
    {
        string abs = path.Length == 0 ? "" : ProjectSettings.GlobalizePath(path);
        return new SourceLicence(abs.Length > 0 && File.Exists(abs) ? TableRows(File.ReadAllLines(abs)) : []);
    }

    public static SourceLicence FromLines(IEnumerable<string> lines) => new(TableRows(lines));

    /// <summary>Matches <paramref name="fileName"/> (a bare name, e.g. <c>sword_hit.wav</c>) against each row's first
    /// cell only, since other cells mention files a row no longer uses.</summary>
    public LicenceStatus StatusFor(string fileName)
    {
        if (fileName.Length == 0)
        {
            return LicenceStatus.Missing;
        }
        foreach ((string firstCell, string row) in _rows)
        {
            if (firstCell.Contains(fileName, StringComparison.Ordinal))
            {
                return row.Contains("Confirm", StringComparison.Ordinal) ? LicenceStatus.Confirm : LicenceStatus.Clean;
            }
        }
        return LicenceStatus.Missing;
    }

    /// <summary>A markdown file's table rows, each with its first cell.</summary>
    private static List<(string FirstCell, string Row)> TableRows(IEnumerable<string> lines)
    {
        var rows = new List<(string, string)>();
        foreach (string line in lines)
        {
            string[] cells = line.Split('|');
            if (line.StartsWith('|') && cells.Length >= 3)
            {
                rows.Add((cells[1], line));
            }
        }
        return rows;
    }
}
