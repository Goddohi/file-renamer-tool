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
        var runs = Cleanup(Align(oldName, newName));
        var oldSegs = new List<DiffSegment>();
        var newSegs = new List<DiffSegment>();
        foreach (var (kind, text) in runs)
        {
            if (kind != DiffKind.Added) Append(oldSegs, text, kind);
            if (kind != DiffKind.Removed) Append(newSegs, text, kind);
        }
        return (oldSegs, newSegs);
    }

    private static List<(DiffKind Kind, string Text)> Align(string a, string b)
    {
        int n = a.Length, m = b.Length, width = m + 1;
        // A match outweighs every possible run penalty: maximize LCS length first,
        // then minimize the number of Same runs. State records whether the preceding
        // operation was Same, so continuing a run does not incur another penalty.
        long matchWeight = (long)Math.Min(n, m) + 1;
        var nextChanged = new long[width];
        var nextSame = new long[width];
        var currentChanged = new long[width];
        var currentSame = new long[width];
        // Two 2-bit traceback choices per cell; scores only need two rolling rows.
        const byte same = 0, removed = 1, added = 2;
        var choices = new byte[checked(n * m)];
        for (int i = n - 1; i >= 0; i--)
        {
            currentChanged[m] = currentSame[m] = 0;
            for (int j = m - 1; j >= 0; j--)
            {
                // Both edits end a Same run. Preserve deletion-first ordering on ties.
                bool remove = nextChanged[j] >= currentChanged[j + 1];
                long editScore = remove ? nextChanged[j] : currentChanged[j + 1];
                byte changedChoice = remove ? removed : added;
                byte sameChoice = changedChoice;
                long changedScore = editScore, sameScore = editScore;
                if (a[i] == b[j])
                {
                    long matchScore = nextSame[j + 1] + matchWeight;
                    if (matchScore - 1 >= changedScore)
                    {
                        changedScore = matchScore - 1;
                        changedChoice = same;
                    }
                    if (matchScore >= sameScore)
                    {
                        sameScore = matchScore;
                        sameChoice = same;
                    }
                }
                currentChanged[j] = changedScore;
                currentSame[j] = sameScore;
                choices[i * m + j] = (byte)(changedChoice | (sameChoice << 2));
            }
            (nextChanged, currentChanged) = (currentChanged, nextChanged);
            (nextSame, currentSame) = (currentSame, nextSame);
        }

        var runs = new List<(DiffKind Kind, string Text)>();
        var text = new System.Text.StringBuilder();
        DiffKind runKind = DiffKind.Same;
        int x = 0, y = 0;
        bool previousSame = false;
        while (x < n && y < m)
        {
            int choice = (choices[x * m + y] >> (previousSame ? 2 : 0)) & 3;
            previousSame = choice == same;
            if (choice == same) { Add(DiffKind.Same, a[x++]); y++; }
            else if (choice == removed) Add(DiffKind.Removed, a[x++]);
            else Add(DiffKind.Added, b[y++]);
        }
        while (x < n) Add(DiffKind.Removed, a[x++]);
        while (y < m) Add(DiffKind.Added, b[y++]);
        if (text.Length > 0) runs.Add((runKind, text.ToString()));
        return runs;

        void Add(DiffKind kind, char c)
        {
            if (text.Length > 0 && runKind != kind)
            {
                runs.Add((runKind, text.ToString()));
                text.Clear();
            }
            runKind = kind;
            text.Append(c);
        }
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
