using FileRenamer.Core;
using Xunit;

namespace FileRenamer.Core.Tests;

public class PlannerExecutorTests
{
    private const string D = @"C:\data";
    private static string P(string n) => Path.Combine(D, n);

    private sealed class Fixed(params string[] names) : IRenameMode
    {
        public IReadOnlyList<string> Apply(IReadOnlyList<string> stems) => names;
    }

    private sealed class FakeFs(params string[] files) : IFileSystem
    {
        public HashSet<string> Files { get; } = new(files, StringComparer.OrdinalIgnoreCase);
        public HashSet<string> FailOnMoveFrom { get; } = new(StringComparer.OrdinalIgnoreCase);
        public void Move(string from, string to)
        {
            if (FailOnMoveFrom.Contains(from)) throw new IOException("사용 중");
            if (!Files.Contains(from)) throw new FileNotFoundException("없음");
            if (Files.Contains(to) && !string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) throw new IOException("exists");
            Files.Remove(from);
            Files.Add(to);
        }
        public bool Exists(string path) => Files.Contains(path);
    }

    private static IReadOnlyList<RenamePlanItem> Plan(string[] files, IRenameMode mode, params string[] onDisk)
        => RenamePlanner.Build(files.Select(P).ToList(), mode, p => onDisk.Select(P).Contains(p, StringComparer.OrdinalIgnoreCase) || files.Select(P).Contains(p, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void ExtensionPreserved()
    {
        var plan = Plan(["a.PDF"], new Fixed("b"));
        Assert.Equal("b.PDF", plan[0].NewName);
        Assert.Equal(PlanStatus.Ok, plan[0].Status);
    }

    // AC 4
    [Fact]
    public void Unchanged_NotConflict()
    {
        var plan = Plan(["a.txt"], new Fixed("a"));
        Assert.Equal(PlanStatus.Unchanged, plan[0].Status);
        Assert.False(RenamePlanner.CanApply(plan));
    }

    // AC 3a
    [Fact]
    public void DuplicateInBatch_CaseInsensitive()
    {
        var plan = Plan(["a.txt", "b.txt"], new Fixed("X", "x"));
        Assert.All(plan, p => Assert.Equal(PlanStatus.DuplicateInBatch, p.Status));
        Assert.False(RenamePlanner.CanApply(plan));
    }

    [Fact]
    public void DifferentExtensionsNotDuplicate()
    {
        var plan = Plan(["a.txt", "b.pdf"], new Fixed("x", "x"));
        Assert.All(plan, p => Assert.Equal(PlanStatus.Ok, p.Status));
    }

    // AC 3b
    [Fact]
    public void ExistsOnDisk_CaseInsensitive()
    {
        var plan = Plan(["a.txt"], new Fixed("OTHER"), "other.txt");
        Assert.Equal(PlanStatus.ExistsOnDisk, plan[0].Status);
        Assert.False(RenamePlanner.CanApply(plan));
    }

    [Fact]
    public void NewNameEqualsOtherBatchFileBeingRenamed_IsOk()
    {
        // swap
        var plan = Plan(["a.txt", "b.txt"], new Fixed("b", "a"));
        Assert.All(plan, p => Assert.Equal(PlanStatus.Ok, p.Status));
        Assert.True(RenamePlanner.CanApply(plan));
    }

    [Fact]
    public void CaseOnlyRename_IsOk()
    {
        var plan = Plan(["a.txt"], new Fixed("A"));
        Assert.Equal(PlanStatus.Ok, plan[0].Status);
    }

    // AC 3c
    [Theory]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("a*")]
    [InlineData("a?")]
    [InlineData("a\"")]
    [InlineData("a<b")]
    [InlineData("a|b")]
    [InlineData("a\\b")]
    [InlineData("")]
    [InlineData("con")]
    [InlineData("LPT1")]
    public void InvalidNames(string stem)
    {
        var plan = Plan(["a.txt"], new Fixed(stem));
        Assert.Equal(PlanStatus.InvalidName, plan[0].Status);
        Assert.False(RenamePlanner.CanApply(plan));
    }

    [Theory]
    [InlineData("a.")]
    [InlineData("a ")]
    public void InvalidNames_TrailingDotOrSpace_WhenNoExtension(string name)
    {
        var plan = Plan(["a"], new Fixed(name));
        Assert.Equal(PlanStatus.InvalidName, plan[0].Status);
    }

    [Fact]
    public void RealModeIntegration_Sequence()
    {
        var plan = Plan(["x.pdf", "y.jpg"], new SequenceMode("보고서", 1, 3, SequenceSeparator.Underscore));
        Assert.Equal(["보고서_001.pdf", "보고서_002.jpg"], plan.Select(p => p.NewName).ToArray());
    }

    // AC 13
    [Fact]
    public void Execute_Swap()
    {
        var fs = new FakeFs(P("a.txt"), P("b.txt"));
        var result = RenameExecutor.Execute(Plan(["a.txt", "b.txt"], new Fixed("b", "a")), fs);
        Assert.Equal(2, result.Succeeded.Count);
        Assert.Empty(result.Failed);
        Assert.Equal(new[] { P("a.txt"), P("b.txt") }.OrderBy(x => x), fs.Files.OrderBy(x => x));
    }

    [Fact]
    public void Execute_CaseOnly()
    {
        var fs = new FakeFs(P("a.txt"));
        var result = RenameExecutor.Execute(Plan(["a.txt"], new Fixed("A")), fs);
        Assert.Single(result.Succeeded);
        Assert.Contains(P("A.txt"), fs.Files);
        Assert.Single(fs.Files);
    }

    // AC 14
    [Fact]
    public void Execute_FailureCollected_OthersContinue()
    {
        var fs = new FakeFs(P("a.txt"), P("b.txt"));
        fs.FailOnMoveFrom.Add(P("a.txt"));
        var result = RenameExecutor.Execute(Plan(["a.txt", "b.txt"], new Fixed("x", "y")), fs);
        Assert.Single(result.Succeeded);
        Assert.Equal((P("b.txt"), P("y.txt")), result.Succeeded[0]);
        Assert.Single(result.Failed);
        Assert.Equal(P("a.txt"), result.Failed[0].Path);
        Assert.Contains("사용 중", result.Failed[0].Reason);
    }

    [Fact]
    public void Execute_SkipsUnchangedAndMissingFileFails()
    {
        var fs = new FakeFs(P("a.txt"));
        var plan = RenamePlanner.Build([P("a.txt"), P("gone.txt")], new Fixed("a", "z"), p => fs.Exists(p));
        var result = RenameExecutor.Execute(plan, fs);
        Assert.Empty(result.Succeeded);
        Assert.Single(result.Failed);
    }

    // AC 15
    [Fact]
    public void Undo_Restores()
    {
        var fs = new FakeFs(P("a.txt"), P("b.txt"));
        var result = RenameExecutor.Execute(Plan(["a.txt", "b.txt"], new Fixed("b", "a")), fs);
        var undo = RenameExecutor.Undo(result, fs);
        Assert.Equal(2, undo.Succeeded.Count);
        Assert.Empty(undo.Failed);
        Assert.Equal(new[] { P("a.txt"), P("b.txt") }.OrderBy(x => x), fs.Files.OrderBy(x => x));
    }

    [Fact]
    public void Undo_ReportsFailure()
    {
        var fs = new FakeFs(P("a.txt"));
        var result = RenameExecutor.Execute(Plan(["a.txt"], new Fixed("x")), fs);
        fs.Files.Remove(P("x.txt"));
        var undo = RenameExecutor.Undo(result, fs);
        Assert.Single(undo.Failed);
    }

    [Fact]
    public void RealDisk_ExecuteSwapAndUndo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "fr_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string a = Path.Combine(dir, "a.txt"), b = Path.Combine(dir, "b.txt");
            File.WriteAllText(a, "A");
            File.WriteAllText(b, "B");
            var plan = RenamePlanner.Build([a, b], new Fixed("b", "a"), File.Exists);
            Assert.True(RenamePlanner.CanApply(plan));
            var res = RenameExecutor.Execute(plan, new PhysicalFileSystem());
            Assert.Empty(res.Failed);
            Assert.Equal("B", File.ReadAllText(a));
            Assert.Equal("A", File.ReadAllText(b));
            Assert.Equal(2, Directory.GetFiles(dir).Length);

            var undo = RenameExecutor.Undo(res, new PhysicalFileSystem());
            Assert.Empty(undo.Failed);
            Assert.Equal("A", File.ReadAllText(a));
            Assert.Equal("B", File.ReadAllText(b));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void RealDisk_CaseOnlyRename()
    {
        string dir = Path.Combine(Path.GetTempPath(), "fr_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string a = Path.Combine(dir, "abc.txt");
            File.WriteAllText(a, "x");
            var plan = RenamePlanner.Build([a], new CaseMode(CaseStyle.Upper), File.Exists);
            Assert.Equal(PlanStatus.Ok, plan[0].Status);
            RenameExecutor.Execute(plan, new PhysicalFileSystem());
            Assert.Equal("ABC.txt", Path.GetFileName(Directory.GetFiles(dir)[0]));
        }
        finally { Directory.Delete(dir, true); }
    }
}
