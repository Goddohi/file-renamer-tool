using FileRenamer.Core;
using Xunit;

namespace FileRenamer.Core.Tests;

/// <summary>QA-authored tests using real temp directories.</summary>
public sealed class QaTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qa_fr_" + Guid.NewGuid().ToString("N"));

    public QaTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private sealed class MapMode(Func<string, string> f) : IRenameMode
    {
        public IReadOnlyList<string> Apply(IReadOnlyList<string> stems) => stems.Select(f).ToList();
    }

    private string Make(string name, string? content = null, string? sub = null)
    {
        string dir = sub == null ? _root : Path.Combine(_root, sub);
        Directory.CreateDirectory(dir);
        string p = Path.Combine(dir, name);
        File.WriteAllText(p, content ?? name);
        return p;
    }

    private static IReadOnlyList<RenamePlanItem> Plan(IReadOnlyList<string> paths, IRenameMode m) =>
        RenamePlanner.Build(paths, m, File.Exists);

    private string[] Names(string? sub = null) =>
        Directory.GetFiles(sub == null ? _root : Path.Combine(_root, sub)).Select(Path.GetFileName).Cast<string>().OrderBy(x => x, StringComparer.Ordinal).ToArray();

    // ---------- file system behavior ----------

    [Fact]
    public void CaseOnlyRename_RealNtfs()
    {
        var p = Make("a.txt");
        var plan = Plan([p], new CaseMode(CaseStyle.Upper));
        Assert.Equal(PlanStatus.Ok, plan[0].Status);
        Assert.Equal("A.txt", plan[0].NewName);
        var r = RenameExecutor.Execute(plan, new PhysicalFileSystem());
        Assert.Empty(r.Failed);
        Assert.Equal(["A.txt"], Names());
    }

    [Fact]
    public void Swap_RealFiles_ContentsSwapped()
    {
        var a = Make("A.txt", "contentA");
        var b = Make("B.txt", "contentB");
        var mode = new MapMode(s => s == "A" ? "B" : "A");
        var plan = Plan([a, b], mode);
        Assert.All(plan, p => Assert.Equal(PlanStatus.Ok, p.Status));
        var r = RenameExecutor.Execute(plan, new PhysicalFileSystem());
        Assert.Empty(r.Failed);
        Assert.Equal("contentA", File.ReadAllText(Path.Combine(_root, "B.txt")));
        Assert.Equal("contentB", File.ReadAllText(Path.Combine(_root, "A.txt")));
        Assert.Equal(["A.txt", "B.txt"], Names());
    }

    [Fact]
    public void Chain_AtoB_BtoC_Real()
    {
        var a = Make("A.txt", "a");
        var b = Make("B.txt", "b");
        var mode = new MapMode(s => s == "A" ? "B" : "C");
        var plan = Plan([a, b], mode);
        Assert.All(plan, p => Assert.Equal(PlanStatus.Ok, p.Status));
        var r = RenameExecutor.Execute(plan, new PhysicalFileSystem());
        Assert.Empty(r.Failed);
        Assert.Equal("a", File.ReadAllText(Path.Combine(_root, "B.txt")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(_root, "C.txt")));
        Assert.Equal(["B.txt", "C.txt"], Names());
    }

    [Fact]
    public void Undo_Swap_RestoresOriginal()
    {
        var a = Make("A.txt", "contentA");
        var b = Make("B.txt", "contentB");
        var fs = new PhysicalFileSystem();
        var r = RenameExecutor.Execute(Plan([a, b], new MapMode(s => s == "A" ? "B" : "A")), fs);
        var u = RenameExecutor.Undo(r, fs);
        Assert.Empty(u.Failed);
        Assert.Equal("contentA", File.ReadAllText(a));
        Assert.Equal("contentB", File.ReadAllText(b));
    }

    [Fact]
    public void Undo_CaseOnlyRename()
    {
        var p = Make("a.txt");
        var fs = new PhysicalFileSystem();
        var r = RenameExecutor.Execute(Plan([p], new CaseMode(CaseStyle.Upper)), fs);
        RenameExecutor.Undo(r, fs);
        Assert.Equal(["a.txt"], Names());
    }

    [Fact]
    public void ExistingFileNotInBatch_IsExistsOnDisk()
    {
        var a = Make("a.txt");
        Make("b.txt");
        var plan = Plan([a], new MapMode(_ => "b"));
        Assert.Equal(PlanStatus.ExistsOnDisk, plan[0].Status);
        Assert.False(RenamePlanner.CanApply(plan));
    }

    [Fact]
    public void ExistingFileNotInBatch_CaseInsensitive()
    {
        var a = Make("a.txt");
        Make("b.txt");
        var plan = Plan([a], new MapMode(_ => "B"));
        Assert.Equal(PlanStatus.ExistsOnDisk, plan[0].Status);
    }

    [Fact]
    public void ExistingFileInBatchBeingRenamedAway_IsOk()
    {
        var a = Make("a.txt", "a");
        var b = Make("b.txt", "b");
        var plan = Plan([a, b], new MapMode(s => s == "a" ? "b" : "c"));
        Assert.All(plan, p => Assert.Equal(PlanStatus.Ok, p.Status));
    }

    [Fact]
    public void ExistingFileInBatchButUnchanged_IsBlocked_NotOk()
    {
        var a = Make("a.txt");
        var b = Make("b.txt");
        var plan = Plan([a, b], new MapMode(s => s == "a" ? "b" : s));
        Assert.False(RenamePlanner.CanApply(plan));
    }

    [Fact]
    public void SameNewNameInDifferentFolders_IsNotConflict()
    {
        var a = Make("a.txt", sub: "d1");
        var b = Make("b.txt", sub: "d2");
        var plan = Plan([a, b], new MapMode(_ => "same"));
        Assert.All(plan, p => Assert.Equal(PlanStatus.Ok, p.Status));
        var r = RenameExecutor.Execute(plan, new PhysicalFileSystem());
        Assert.Empty(r.Failed);
        Assert.Equal(["same.txt"], Names("d1"));
        Assert.Equal(["same.txt"], Names("d2"));
    }

    [Fact]
    public void DuplicateInBatch_CaseInsensitive()
    {
        var a = Make("a.txt");
        var b = Make("b.txt");
        var plan = Plan([a, b], new MapMode(s => s == "a" ? "X" : "x"));
        Assert.Equal(PlanStatus.DuplicateInBatch, plan[0].Status);
        Assert.Equal(PlanStatus.DuplicateInBatch, plan[1].Status);
    }

    [Fact]
    public void DirectoryWithTargetName_IsConflict_AsVmWouldSeeIt()
    {
        // MainViewModel passes File.Exists as the existence probe; a directory named "b.txt" is not detected.
        var a = Make("a.txt");
        Directory.CreateDirectory(Path.Combine(_root, "b.txt"));
        var plan = RenamePlanner.Build([a], new MapMode(_ => "b"), File.Exists);
        Assert.NotEqual(PlanStatus.Ok, plan[0].Status);
    }

    [Fact]
    public void DirectoryWithTargetName_ApplyFailsGracefullyAndKeepsFile()
    {
        var a = Make("a.txt");
        Directory.CreateDirectory(Path.Combine(_root, "b.txt"));
        // Directory appeared after the preview was built (race): the item is still Ok in the stale plan.
        var plan = new[] { new RenamePlanItem(a, "b.txt", PlanStatus.Ok) };
        var r = RenameExecutor.Execute(plan, new PhysicalFileSystem());
        Assert.Single(r.Failed);
        Assert.True(File.Exists(a));
        Assert.Empty(Directory.GetFiles(_root, "~rn_*"));
    }

    [Fact]
    public void LockedFile_OthersContinue_FailureListed_UndoOnlySucceeded()
    {
        var a = Make("a.txt");
        var b = Make("b.txt");
        var c = Make("c.txt");
        var fs = new PhysicalFileSystem();
        RenameResult r;
        using (new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            r = RenameExecutor.Execute(Plan([a, b, c], new MapMode(s => s + "_n")), fs);
        }
        Assert.Single(r.Failed);
        Assert.Equal(b, r.Failed[0].Path);
        Assert.Equal(2, r.Succeeded.Count);
        Assert.Equal(["a_n.txt", "b.txt", "c_n.txt"], Names());

        var u = RenameExecutor.Undo(r, fs);
        Assert.Empty(u.Failed);
        Assert.Equal(["a.txt", "b.txt", "c.txt"], Names());
        Assert.Empty(Directory.GetFiles(_root, "~rn_*"));
    }

    [Fact]
    public void LockedFile_TargetOfAnotherRename_RollsBackNoLoss()
    {
        // a -> b ; b -> c where b is locked. a's move must fail and a must remain a.txt.
        var a = Make("a.txt", "A");
        var b = Make("b.txt", "B");
        var fs = new PhysicalFileSystem();
        RenameResult r;
        using (new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            r = RenameExecutor.Execute(Plan([a, b], new MapMode(s => s == "a" ? "b" : "c")), fs);
        }
        Assert.Equal("A", File.ReadAllText(Path.Combine(_root, "a.txt")));
        Assert.Equal("B", File.ReadAllText(Path.Combine(_root, "b.txt")));
        Assert.Equal(2, r.Failed.Count);
        Assert.Empty(Directory.GetFiles(_root, "~rn_*"));
    }

    [Fact]
    public void SourceDeletedAfterPlan_ReportedOthersContinue()
    {
        var a = Make("a.txt");
        var b = Make("b.txt");
        var plan = Plan([a, b], new MapMode(s => s + "_n"));
        File.Delete(a);
        var r = RenameExecutor.Execute(plan, new PhysicalFileSystem());
        Assert.Single(r.Failed);
        Assert.Single(r.Succeeded);
        Assert.True(File.Exists(Path.Combine(_root, "b_n.txt")));
    }

    // ---------- planner validation ----------

    [Theory]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("Com1")]
    [InlineData("LPT9")]
    public void ReservedNames_Invalid(string newStem)
    {
        var a = Make("a.txt");
        var plan = RenamePlanner.Build([a], new MapMode(_ => newStem), _ => false);
        Assert.Equal(PlanStatus.InvalidName, plan[0].Status);
    }

    [Fact]
    public void TrailingDotOrSpace_NoExtension_Invalid()
    {
        var a = Make("a");
        Assert.Equal(PlanStatus.InvalidName, RenamePlanner.Build([a], new MapMode(_ => "x."), _ => false)[0].Status);
        Assert.Equal(PlanStatus.InvalidName, RenamePlanner.Build([a], new MapMode(_ => "x "), _ => false)[0].Status);
    }

    [Fact]
    public void EmptyStem_Invalid()
    {
        var a = Make("a.txt");
        Assert.Equal(PlanStatus.InvalidName, Plan([a], new ReplaceMode("a", "", false))[0].Status);
    }

    [Theory]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("a?b")]
    [InlineData("a\"b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a|b")]
    [InlineData("a/b")]
    public void ForbiddenChars_Invalid(string stem)
    {
        var a = Make("a.txt");
        Assert.Equal(PlanStatus.InvalidName, RenamePlanner.Build([a], new MapMode(_ => stem), _ => false)[0].Status);
    }

    [Fact]
    public void OverlongName_ShouldBeInvalid()
    {
        var a = Make("a.txt");
        var plan = Plan([a], new SequenceMode(new string('x', 300), 1, 1, SequenceSeparator.Space));
        Assert.Equal(PlanStatus.InvalidName, plan[0].Status);
    }

    [Fact]
    public void MultiDot_DotFile_NoExtension_Split()
    {
        Assert.Equal(("a.b.c", ".txt"), FileNameSplitter.Split("a.b.c.txt"));
        Assert.Equal((".gitignore", ""), FileNameSplitter.Split(".gitignore"));
        Assert.Equal(("README", ""), FileNameSplitter.Split("README"));
    }

    [Fact]
    public void MultiDot_ExtensionPreserved_ThroughPlanner()
    {
        var p1 = Make("My.REPORT.final.PDF");
        var p2 = Make(".gitignore");
        var p3 = Make("NOEXT");
        var plan = Plan([p1, p2, p3], new CaseMode(CaseStyle.Lower));
        Assert.Equal("my.report.final.PDF", plan[0].NewName);
        Assert.Equal(PlanStatus.Unchanged, plan[1].Status);
        Assert.Equal("noext", plan[2].NewName);
    }

    // ---------- modes ----------

    [Fact]
    public void Regex_Invalid_Throws() =>
        Assert.Throws<RenameRuleException>(() => new ReplaceMode("(", "", true).Apply(["a"]));

    [Fact]
    public void Regex_GroupReference()
    {
        Assert.Equal(["2026-10"], new ReplaceMode(@"(\d{4})(\d{2})", "$1-$2", true).Apply(["202610"]));
    }

    [Fact]
    public void Regex_DigitsRemoved_Spec()
    {
        Assert.Equal(["보고서"], new ReplaceMode(@"\d+", "", true).Apply(["보고서123"]));
    }

    [Fact]
    public void Replace_Plain_SpecExample_AndRegexCharsLiteral()
    {
        Assert.Equal(["보고서"], new ReplaceMode("(최종)", "", false).Apply(["보고서(최종)"]));
        Assert.Equal(["a-b"], new ReplaceMode(".", "-", false).Apply(["a.b"]));
    }

    [Fact]
    public void Replace_Plain_DollarInReplacementIsLiteral()
    {
        Assert.Equal(["a$1b"], new ReplaceMode("-", "$1", false).Apply(["a-b"]));
    }

    [Fact]
    public void Template_SpecExample_And_CustomDateFormat()
    {
        var s = new AppSettings { DateFormat = "yyyy-MM-dd", CompanyName = "오르카", UserName = "최경태" };
        var r = new TemplateMode("{날짜}_{회사명}_{파일명}_{이름}", s, new DateTime(2026, 10, 22)).Apply(["견적서"]);
        Assert.Equal(["2026-10-22_오르카_견적서_최경태"], r);
        var s2 = s with { DateFormat = "yyMMdd" };
        Assert.Equal(["261022"], new TemplateMode("{날짜}", s2, new DateTime(2026, 10, 22)).Apply(["x"]));
    }

    [Fact]
    public void Template_Unknown_Throws_And_LiteralBracesKept()
    {
        var s = new AppSettings();
        Assert.Throws<RenameRuleException>(() => new TemplateMode("{abc}", s, DateTime.Today).Apply(["x"]));
        Assert.Throws<RenameRuleException>(() => new TemplateMode("{}", s, DateTime.Today).Apply(["x"]));
        Assert.Equal(["{_q"], new TemplateMode("{_{파일명}", s, DateTime.Today).Apply(["q"]));
    }

    [Fact]
    public void Template_InvalidDateFormat_Throws()
    {
        var s = new AppSettings { DateFormat = "%" };
        Assert.Throws<RenameRuleException>(() => new TemplateMode("{날짜}", s, DateTime.Today).Apply(["x"]));
    }

    [Fact]
    public void Template_DateFormatWithSlash_FlaggedInvalidByPlanner()
    {
        var a = Make("a.txt");
        var s = new AppSettings { DateFormat = "yyyy/MM/dd" };
        var plan = Plan([a], new TemplateMode("{날짜}", s, DateTime.Today));
        Assert.Equal(PlanStatus.InvalidName, plan[0].Status);
    }

    [Fact]
    public void Case_Hangul_Untouched_Mixed()
    {
        Assert.Equal(["보고서 final"], new CaseMode(CaseStyle.Lower).Apply(["보고서 FINAL"]));
        Assert.Equal(["보고서 FINAL"], new CaseMode(CaseStyle.Upper).Apply(["보고서 final"]));
        Assert.Equal(["보고서 final"], new CaseMode(CaseStyle.FirstUpper).Apply(["보고서 FINAL"]));
        Assert.Equal(["보고서 Final"], new CaseMode(CaseStyle.TitleCase).Apply(["보고서 FINAL"]));
    }

    [Fact]
    public void Case_Title_Separators()
    {
        Assert.Equal(["My Report_Final-Draft"], new CaseMode(CaseStyle.TitleCase).Apply(["my REPORT_final-draft"]));
        Assert.Equal(["A.b"], new CaseMode(CaseStyle.TitleCase).Apply(["a.B"]));
    }

    [Fact]
    public void Case_Spec_FirstUpper_WithExtension()
    {
        var p = Make("my REPORT_final.PDF");
        var plan = Plan([p], new CaseMode(CaseStyle.FirstUpper));
        Assert.Equal("My report_final.PDF", plan[0].NewName);
    }

    [Fact]
    public void Sequence_Variants()
    {
        Assert.Equal(["보고서_001", "보고서_002"], new SequenceMode("보고서", 1, 3, SequenceSeparator.Underscore).Apply(["a", "b"]));
        Assert.Equal(["x-10", "x-11"], new SequenceMode("x", 10, 1, SequenceSeparator.Hyphen).Apply(["a", "b"]));
        Assert.Equal(["x1"], new SequenceMode("x", 1, 1, SequenceSeparator.None).Apply(["a"]));
        Assert.Equal(["x 1"], new SequenceMode("x", 1, 0, SequenceSeparator.Space).Apply(["a"]));
        Assert.Equal(["x_0099", "x_0100"], new SequenceMode("x", 99, 4, SequenceSeparator.Underscore).Apply(["a", "b"]));
    }

    [Fact]
    public void Sequence_KeepsEachExtension_AndOrder()
    {
        var a = Make("z.pdf");
        var b = Make("y.jpg");
        var plan = Plan([a, b], new SequenceMode("보고서", 1, 3, SequenceSeparator.Underscore));
        Assert.Equal("보고서_001.pdf", plan[0].NewName);
        Assert.Equal("보고서_002.jpg", plan[1].NewName);
    }

    [Fact]
    public void Sequence_AlreadyNamed_RerunIsUnchanged()
    {
        var a = Make("보고서_001.pdf");
        var plan = Plan([a], new SequenceMode("보고서", 1, 3, SequenceSeparator.Underscore));
        Assert.Equal(PlanStatus.Unchanged, plan[0].Status);
    }

    [Fact]
    public void Replace_EmptyFind_NoOp_Regex_And_Plain()
    {
        Assert.Equal(["a"], new ReplaceMode("", "x", false).Apply(["a"]));
        Assert.Equal(["a"], new ReplaceMode("", "x", true).Apply(["a"]));
    }

    // ---------- folder expansion / settings ----------

    [Fact]
    public void FolderExpander_OnlyDirectChildren_NoDuplicates()
    {
        var f1 = Make("a.txt");
        Make("deep.txt", sub: "sub");
        var r = FolderExpander.Expand([_root, f1, f1.ToUpperInvariant()]);
        Assert.Single(r);
        Assert.Equal(f1, r[0]);
    }

    [Fact]
    public void Settings_RoundTrip_Hangul_TempPath()
    {
        var path = Path.Combine(_root, "cfg", "settings.json");
        var st = new SettingsStore(path);
        var s = new AppSettings { CompanyName = "오르카", UserName = "최경태", Template = "{날짜}-{파일명}", SelectedMode = 3, SequenceDigits = 4 };
        st.Save(s);
        Assert.Equal(s, st.Load());
        Assert.Contains("오르카", File.ReadAllText(path));
    }

    [Fact]
    public void Settings_Corrupt_FallsBackToDefault()
    {
        var path = Path.Combine(_root, "bad.json");
        File.WriteAllText(path, "{ not json");
        Assert.Equal(new AppSettings(), new SettingsStore(path).Load());
    }

    [Fact]
    public void Settings_NullStringsInJson_DoNotProduceNulls()
    {
        // A hand-edited file with "Template": null would crash the App (NRE in Refresh, which only catches RenameRuleException).
        var path = Path.Combine(_root, "nulls.json");
        File.WriteAllText(path, "{\"Template\":null,\"ReplaceFind\":null,\"ReplaceWith\":null,\"SequenceBaseName\":null}");
        var s = new SettingsStore(path).Load();
        Assert.NotNull(s.Template);
        Assert.NotNull(s.ReplaceFind);
        Assert.NotNull(s.ReplaceWith);
        Assert.NotNull(s.SequenceBaseName);
    }

    [Fact]
    public void Sequence_HugeDigits_DoesNotThrow()
    {
        // App's Refresh only catches RenameRuleException; arbitrary digits must not blow up.
        var ex = Record.Exception(() => new SequenceMode("x", 1, 100_000_000, SequenceSeparator.None).Apply(["a"]));
        Assert.True(ex == null || ex is RenameRuleException, ex?.GetType().Name);
    }
}
