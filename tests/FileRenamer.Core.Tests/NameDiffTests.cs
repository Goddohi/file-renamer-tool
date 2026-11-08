using FileRenamer.Core;
using Xunit;

namespace FileRenamer.Core.Tests;

public class NameDiffTests
{
    private static string Show(IReadOnlyList<DiffSegment> segs) => string.Concat(segs.Select(s => s.Kind switch
    {
        DiffKind.Removed => "[-" + s.Text + "]",
        DiffKind.Added => "[+" + s.Text + "]",
        _ => s.Text,
    }));

    [Theory]
    [InlineData("보고서(최종).docx", "보고서.docx", "보고서[-(최종)].docx", "보고서.docx")]
    [InlineData("보고서(최종).docx", "보고서_v2.docx", "보고서[-(최종)].docx", "보고서[+_v2].docx")]
    [InlineData("견적서.xlsx", "20261023_오르카_견적서_홍길동.xlsx", "견적서.xlsx", "[+20261023_오르카_]견적서[+_홍길동].xlsx")]
    [InlineData("IMG_2041.jpg", "보고서_001.jpg", "[-IMG_204]1.jpg", "[+보고서_00]1.jpg")]
    public void Highlights(string oldName, string newName, string expectedOld, string expectedNew)
    {
        var (o, n) = NameDiff.Compute(oldName, newName);
        Assert.Equal(expectedOld, Show(o));
        Assert.Equal(expectedNew, Show(n));
    }

    [Theory]
    [InlineData("을지대학원지원_학업계획서.hwp", "[+을지대학원지원_]학업계획서.hwp")]
    [InlineData("을지대학원_학업계획서_최종.hwp", "[+을지대학원_]학업계획서[+_최종].hwp")]
    public void AddedPrefixWithRepeatedCharacter_PreservesOriginalRuns(string newName, string expectedNew)
    {
        const string oldName = "학업계획서.hwp";
        var (o, n) = NameDiff.Compute(oldName, newName);
        Assert.DoesNotContain(o, s => s.Kind == DiffKind.Removed);
        Assert.Equal(oldName, Show(o));
        Assert.Equal(expectedNew, Show(n));
    }

    [Fact]
    public void InsertionsWithLongerMisleadingSubstring_DoNotRemoveOriginalCharacters()
    {
        const string oldName = "aabbccdd.txt";
        const string newName = "bbccddXaaYbbZccQdd.txt";
        var (o, n) = NameDiff.Compute(oldName, newName);
        Assert.DoesNotContain(o, s => s.Kind == DiffKind.Removed);
        Assert.Equal(oldName, Show(o));
        Assert.Equal("[+bbccddX]aa[+Y]bb[+Z]cc[+Q]dd.txt", Show(n));
        Assert.Equal(newName, string.Concat(n.Select(s => s.Text)));
    }

    [Fact]
    public void Unchanged_IsSingleSameSegment()
    {
        var (o, n) = NameDiff.Compute("회의록.docx", "회의록.docx");
        Assert.Equal([new DiffSegment("회의록.docx", DiffKind.Same)], o);
        Assert.Equal([new DiffSegment("회의록.docx", DiffKind.Same)], n);
    }

    [Theory]
    [InlineData("my REPORT_final.PDF", "My Report_Final.PDF")]
    [InlineData("", "a.txt")]
    [InlineData("a.txt", "")]
    [InlineData("abc", "xyz")]
    public void SegmentsRebuildBothNames(string oldName, string newName)
    {
        var (o, n) = NameDiff.Compute(oldName, newName);
        Assert.Equal(oldName, string.Concat(o.Select(s => s.Text)));
        Assert.Equal(newName, string.Concat(n.Select(s => s.Text)));
        Assert.DoesNotContain(o, s => s.Kind == DiffKind.Added);
        Assert.DoesNotContain(n, s => s.Kind == DiffKind.Removed);
    }
}
