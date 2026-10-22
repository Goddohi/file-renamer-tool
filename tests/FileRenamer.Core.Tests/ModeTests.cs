using FileRenamer.Core;
using Xunit;

namespace FileRenamer.Core.Tests;

public class ModeTests
{
    private static string One(IRenameMode m, string stem) => m.Apply([stem])[0];

    [Theory]
    [InlineData(".gitignore", ".gitignore", "")]
    [InlineData("a.tar.gz", "a.tar", ".gz")]
    [InlineData("noext", "noext", "")]
    [InlineData("견적서.xlsx", "견적서", ".xlsx")]
    public void Splitter(string name, string stem, string ext)
        => Assert.Equal((stem, ext), FileNameSplitter.Split(name));

    // AC 5
    [Fact]
    public void Replace_Remove() => Assert.Equal("보고서", One(new ReplaceMode("(최종)", "", false), "보고서(최종)"));

    [Fact]
    public void Replace_PlainIsLiteralAndReplacesAll()
        => Assert.Equal("b.b.", One(new ReplaceMode("a.", "b.", false), "a.a."));

    [Fact]
    public void Replace_EmptyFindNoChange() => Assert.Equal("abc", One(new ReplaceMode("", "x", false), "abc"));

    // AC 6
    [Fact]
    public void Replace_RegexDigits() => Assert.Equal("보고서", One(new ReplaceMode(@"\d+", "", true), "보고서123"));

    [Fact]
    public void Replace_RegexGroup() => Assert.Equal("b-a", One(new ReplaceMode(@"(a)-(b)", "$2-$1", true), "a-b"));

    // AC 7
    [Fact]
    public void Replace_BadRegexThrows()
        => Assert.Throws<RenameRuleException>(() => new ReplaceMode("(", "", true).Apply(["x"]));

    // AC 8
    [Fact]
    public void Template_Example()
    {
        var s = new AppSettings { CompanyName = "오르카", UserName = "최경태" };
        var m = new TemplateMode("{날짜}_{회사명}_{파일명}_{이름}", s, new DateTime(2026, 10, 22));
        var (stem, ext) = FileNameSplitter.Split("견적서.xlsx");
        Assert.Equal("20261022_오르카_견적서_최경태.xlsx", One(m, stem) + ext);
    }

    [Fact]
    public void Template_CustomDateFormatAndLiteral()
    {
        var s = new AppSettings { DateFormat = "yyyy-MM-dd" };
        Assert.Equal("[2026-10-22] a", One(new TemplateMode("[{날짜}] {파일명}", s, new DateTime(2026, 10, 22)), "a"));
    }

    // AC 9
    [Fact]
    public void Template_UnknownVariableThrows()
        => Assert.Throws<RenameRuleException>(() => new TemplateMode("{abc}", new AppSettings(), DateTime.Today).Apply(["x"]));

    [Fact]
    public void Template_BadDateFormatThrows()
        => Assert.Throws<RenameRuleException>(() => new TemplateMode("{날짜}", new AppSettings { DateFormat = "%" }, DateTime.Today).Apply(["x"]));

    // AC 10
    [Fact]
    public void Case_FirstUpperExample()
    {
        var (stem, ext) = FileNameSplitter.Split("my REPORT_final.PDF");
        Assert.Equal("My report_final.PDF", One(new CaseMode(CaseStyle.FirstUpper), stem) + ext);
    }

    [Theory]
    [InlineData(CaseStyle.Lower, "Hello WORLD", "hello world")]
    [InlineData(CaseStyle.Upper, "Hello world", "HELLO WORLD")]
    [InlineData(CaseStyle.TitleCase, "my REPORT_final-v2 draft", "My Report_Final-V2 Draft")]
    [InlineData(CaseStyle.TitleCase, "보고서 abc", "보고서 Abc")]
    [InlineData(CaseStyle.Upper, "보고서a", "보고서A")]
    public void Case_Styles(CaseStyle style, string input, string expected)
        => Assert.Equal(expected, One(new CaseMode(style), input));

    // AC 11
    [Fact]
    public void Sequence_Example()
    {
        var r = new SequenceMode("보고서", 1, 3, SequenceSeparator.Underscore).Apply(["a", "b", "c"]);
        Assert.Equal(["보고서_001", "보고서_002", "보고서_003"], r);
    }

    [Theory]
    [InlineData(SequenceSeparator.Space, "x 5")]
    [InlineData(SequenceSeparator.Hyphen, "x-5")]
    [InlineData(SequenceSeparator.None, "x5")]
    public void Sequence_Separators(SequenceSeparator sep, string expected)
        => Assert.Equal(expected, new SequenceMode("x", 5, 1, sep).Apply(["a"])[0]);

    [Fact]
    public void Sequence_DigitsOverflowKeepsFullNumber()
        => Assert.Equal("x 1000", new SequenceMode("x", 1000, 2, SequenceSeparator.Space).Apply(["a"])[0]);
}
