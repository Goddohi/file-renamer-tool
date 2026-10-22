namespace FileRenamer.Core;

public enum PlanStatus { Ok, Unchanged, DuplicateInBatch, ExistsOnDisk, InvalidName }

public sealed record RenamePlanItem(string SourcePath, string NewName, PlanStatus Status);

public static class RenamePlanner
{
    private static readonly char[] InvalidChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static IReadOnlyList<RenamePlanItem> Build(IReadOnlyList<string> paths, IRenameMode mode, Func<string, bool> fileExists)
    {
        var splits = paths.Select(p => FileNameSplitter.Split(Path.GetFileName(p))).ToList();
        var newStems = mode.Apply(splits.Select(s => s.Stem).ToList());
        if (newStems.Count != paths.Count)
            throw new InvalidOperationException("Mode returned a different number of names.");

        var newNames = new string[paths.Count];
        for (int i = 0; i < paths.Count; i++) newNames[i] = newStems[i] + splits[i].Ext;

        string Key(int i) => Path.Combine(Path.GetDirectoryName(paths[i]) ?? "", newNames[i]).ToUpperInvariant();

        var counts = new Dictionary<string, int>();
        for (int i = 0; i < paths.Count; i++)
        {
            string k = Key(i);
            counts[k] = counts.GetValueOrDefault(k) + 1;
        }

        var sources = new HashSet<string>(paths.Select(p => p.ToUpperInvariant()));

        var plan = new List<RenamePlanItem>(paths.Count);
        for (int i = 0; i < paths.Count; i++)
        {
            string name = newNames[i];
            string oldName = Path.GetFileName(paths[i]);
            PlanStatus status;
            if (string.IsNullOrWhiteSpace(newStems[i]) || IsInvalidName(name))
                status = PlanStatus.InvalidName;
            else if (string.Equals(name, oldName, StringComparison.Ordinal))
                status = PlanStatus.Unchanged;
            else if (counts[Key(i)] > 1)
                status = PlanStatus.DuplicateInBatch;
            else
            {
                string target = Path.Combine(Path.GetDirectoryName(paths[i]) ?? "", name);
                status = !sources.Contains(target.ToUpperInvariant()) && (fileExists(target) || Directory.Exists(target))
                    ? PlanStatus.ExistsOnDisk
                    : PlanStatus.Ok;
            }
            plan.Add(new RenamePlanItem(paths[i], name, status));
        }
        return plan;
    }

    public static bool CanApply(IReadOnlyList<RenamePlanItem> plan) =>
        plan.All(p => p.Status is PlanStatus.Ok or PlanStatus.Unchanged) && plan.Any(p => p.Status == PlanStatus.Ok);

    private static bool IsInvalidName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Trim().Length == 0) return true;
        if (name.IndexOfAny(InvalidChars) >= 0) return true;
        if (name.Length > 255) return true;
        if (name.Any(char.IsControl)) return true;
        if (name.EndsWith('.') || name.EndsWith(' ')) return true;
        string stem = name.Contains('.') ? name[..name.IndexOf('.')] : name;
        return Reserved.Contains(stem.TrimEnd());
    }
}
