namespace FileRenamer.Core;

public sealed record RenameFailure(string Path, string Reason);

public sealed record RenameResult(IReadOnlyList<(string From, string To)> Succeeded, IReadOnlyList<RenameFailure> Failed);

public interface IFileSystem
{
    void Move(string from, string to);
    bool Exists(string path);
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public void Move(string from, string to) => File.Move(from, to);
    public bool Exists(string path) => File.Exists(path);
}

public static class RenameExecutor
{
    public static RenameResult Execute(IReadOnlyList<RenamePlanItem> plan, IFileSystem fs)
    {
        var moves = plan
            .Where(p => p.Status == PlanStatus.Ok)
            .Select(p => (From: p.SourcePath, To: Path.Combine(Path.GetDirectoryName(p.SourcePath) ?? "", p.NewName)))
            .ToList();
        return TwoPhase(moves, fs);
    }

    public static RenameResult Undo(RenameResult last, IFileSystem fs) =>
        TwoPhase(last.Succeeded.Select(s => (From: s.To, To: s.From)).ToList(), fs);

    private static RenameResult TwoPhase(List<(string From, string To)> moves, IFileSystem fs)
    {
        var succeeded = new List<(string From, string To)>();
        var failed = new List<RenameFailure>();
        var staged = new List<(string From, string Temp, string To)>();

        foreach (var (from, to) in moves)
        {
            string temp = UniqueTemp(from, fs);
            try
            {
                fs.Move(from, temp);
                staged.Add((from, temp, to));
            }
            catch (Exception ex)
            {
                failed.Add(new RenameFailure(from, ex.Message));
            }
        }

        foreach (var (from, temp, to) in staged)
        {
            try
            {
                fs.Move(temp, to);
                succeeded.Add((from, to));
            }
            catch (Exception ex)
            {
                string reason = ex.Message;
                try { fs.Move(temp, from); }
                catch (Exception rex) { reason += $" (원래 이름 복구 실패: {rex.Message}; 임시 파일: {temp})"; }
                failed.Add(new RenameFailure(from, reason));
            }
        }

        return new RenameResult(succeeded, failed);
    }

    private static string UniqueTemp(string path, IFileSystem fs)
    {
        string dir = Path.GetDirectoryName(path) ?? "";
        string temp;
        do
        {
            temp = Path.Combine(dir, "~rn_" + Guid.NewGuid().ToString("N") + ".tmp");
        } while (fs.Exists(temp));
        return temp;
    }
}
