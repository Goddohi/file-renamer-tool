namespace FileRenamer.Core;

public enum CaseStyle { Lower, Upper, FirstUpper, TitleCase }

public enum SequenceSeparator { Space, Underscore, Hyphen, None }

public sealed record AppSettings
{
    public string DateFormat { get; init; } = "yyyyMMdd";
    public string CompanyName { get; init; } = "";
    public string UserName { get; init; } = "";

    // Last-used inputs. SelectedMode: 0=replace, 1=template, 2=case, 3=sequence
    public int SelectedMode { get; init; } = 0;
    public string ReplaceFind { get; init; } = "";
    public string ReplaceWith { get; init; } = "";
    public bool UseRegex { get; init; } = false;
    public string Template { get; init; } = "{날짜}_{회사명}_{파일명}_{이름}";
    public CaseStyle CaseStyle { get; init; } = CaseStyle.Lower;
    public string SequenceBaseName { get; init; } = "";
    public int SequenceStart { get; init; } = 1;
    public int SequenceDigits { get; init; } = 1;
    public SequenceSeparator SequenceSeparator { get; init; } = SequenceSeparator.Space;
}
