using System.Collections.Generic;
using System.Text;

namespace Soverance.SoundBoard;

/// <summary>The checks of one <c>--board-test</c> run, printed as one line: <c>[board-test] PASS name=detail …</c>.</summary>
public sealed class BoardTestReport
{
    private readonly List<(string Name, bool Pass, string Detail)> _checks = [];

    public bool Passed
    {
        get
        {
            foreach ((string _, bool pass, string _) in _checks)
            {
                if (!pass)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>Records one check; <paramref name="detail"/> is what it found ("ok", "39/39", "missing:cue:step").</summary>
    public void Check(string name, bool pass, string detail) => _checks.Add((name, pass, detail.Replace(' ', '_')));

    public override string ToString()
    {
        var line = new StringBuilder("[board-test] ").Append(Passed ? "PASS" : "FAIL");
        foreach ((string name, bool pass, string detail) in _checks)
        {
            line.Append(' ').Append(name).Append('=').Append(detail);
            if (!pass)
            {
                line.Append("(FAIL)");
            }
        }
        return line.ToString();
    }
}
