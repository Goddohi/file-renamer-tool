namespace FileRenamer.Core;

public enum DiffKind { Same, Removed, Added }

public sealed record DiffSegment(string Text, DiffKind Kind);

public static class NameDiff
{
    /// <summary>
    /// Character-level diff between two names. Old holds Same/Removed segments, New holds Same/Added.
    /// Single-character matches stranded between changes are folded into the change so the highlight
    /// stays readable (e.g. "IMG_2041" vs "보고서_001" is one change, not five fragments).
    /// </summary>
    public static (IReadOnlyList<DiffSegment> Old, IReadOnlyList<DiffSegment> New) Compute(string oldName, string newName)
    {
        var runs = Cleanup(Lcs(oldName, newName));
        var oldSegs = new List<DiffSegment>();
        var newSegs = new List<DiffSegment>();
        foreach (var (kind, text) in runs)
        {
            if (kind != DiffKind.Added) Append(oldSegs, text, kind);
            if (kind != DiffKind.Removed) Append(newSegs, text, kind);
        }
        return (oldSegs, newSegs);
    }

    private static List<(DiffKind Kind, string Text)> Lcs(string a, string b)
    {
        int n = a.Length, m = b.Length;
        var len = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                len[i, j] = a[i] == b[j] ? len[i + 1, j + 1] + 1 : Math.Max(len[i + 1, j], len[i, j + 1]);

        var ops = new List<(DiffKind, char)>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y]) { ops.Add((DiffKind.Same, a[x])); x++; y++; }
            else if (len[x + 1, y] >= len[x, y + 1]) ops.Add((DiffKind.Removed, a[x++]));
            else ops.Add((DiffKind.Added, b[y++]));
        }
        while (x < n) ops.Add((DiffKind.Removed, a[x++]));
        while (y < m) ops.Add((DiffKind.Added, b[y++]));

        var runs = new List<(DiffKind Kind, string Text)>();
        foreach (var (kind, c) in ops)
        {
            if (runs.Count > 0 && runs[^1].Kind == kind) runs[^1] = (kind, runs[^1].Text + c);
            else runs.Add((kind, c.ToString()));
        }
        return runs;
    }

    private static List<(DiffKind Kind, string Text)> Cleanup(List<(DiffKind Kind, string Text)> runs)
    {
        var result = new List<(DiffKind Kind, string Text)>();
        for (int i = 0; i < runs.Count; i++)
        {
            var (kind, text) = runs[i];
            bool stranded = kind == DiffKind.Same && text.Length < 2 && i > 0 && i < runs.Count - 1;
            if (stranded)
            {
                result.Add((DiffKind.Removed, text));
                result.Add((DiffKind.Added, text));
            }
            else result.Add((kind, text));
        }
        return result;
    }

    private static void Append(List<DiffSegment> segs, string text, DiffKind kind)
    {
        if (segs.Count > 0 && segs[^1].Kind == kind) segs[^1] = segs[^1] with { Text = segs[^1].Text + text };
        else segs.Add(new DiffSegment(text, kind));
    }
}
