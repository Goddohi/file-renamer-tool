namespace FileRenamer.Core;

public static class FolderExpander
{
    public static IReadOnlyList<string> Expand(IEnumerable<string> droppedPaths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var p in droppedPaths)
        {
            IEnumerable<string> files;
            if (Directory.Exists(p))
            {
                try
                {
                    files = Directory.GetFiles(p).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
                }
                catch (Exception) { continue; }
            }
            else if (File.Exists(p)) files = [p];
            else continue;

            foreach (var f in files)
                if (seen.Add(f)) result.Add(f);
        }
        return result;
    }
}
