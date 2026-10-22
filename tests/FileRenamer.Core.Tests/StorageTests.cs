using FileRenamer.Core;
using Xunit;

namespace FileRenamer.Core.Tests;

public class StorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fr_test_" + Guid.NewGuid().ToString("N"));

    public StorageTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    // AC 1
    [Fact]
    public void FolderExpander_TopLevelFilesOnly_Dedupe()
    {
        string f1 = Path.Combine(_dir, "b.txt"), f2 = Path.Combine(_dir, "a.txt");
        File.WriteAllText(f1, "");
        File.WriteAllText(f2, "");
        Directory.CreateDirectory(Path.Combine(_dir, "sub"));
        File.WriteAllText(Path.Combine(_dir, "sub", "deep.txt"), "");

        var result = FolderExpander.Expand([_dir, f1, f1.ToUpperInvariant(), Path.Combine(_dir, "missing.txt")]);
        Assert.Equal(2, result.Count);
        Assert.Contains(f1, result, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(f2, result, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(result, r => r.Contains("deep"));
    }

    // AC 17
    [Fact]
    public void Settings_RoundTrip()
    {
        var store = new SettingsStore(Path.Combine(_dir, "sub", "settings.json"));
        var s = new AppSettings
        {
            DateFormat = "yyMMdd", CompanyName = "오르카", UserName = "최경태", SelectedMode = 3,
            Template = "{날짜}_{파일명}", UseRegex = true, CaseStyle = CaseStyle.TitleCase,
            SequenceBaseName = "보고서", SequenceStart = 5, SequenceDigits = 3, SequenceSeparator = SequenceSeparator.Hyphen,
        };
        store.Save(s);
        Assert.Equal(s, store.Load());
    }

    [Fact]
    public void Settings_MissingOrCorrupt_Defaults()
    {
        string path = Path.Combine(_dir, "settings.json");
        Assert.Equal(new AppSettings(), new SettingsStore(path).Load());
        File.WriteAllText(path, "{ not json");
        Assert.Equal(new AppSettings(), new SettingsStore(path).Load());
    }
}
