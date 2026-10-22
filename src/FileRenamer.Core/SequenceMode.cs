namespace FileRenamer.Core;

public sealed class SequenceMode(string baseName, int start, int digits, SequenceSeparator sep) : IRenameMode
{
    public IReadOnlyList<string> Apply(IReadOnlyList<string> stems)
    {
        if (digits < 0 || digits > 10)
            throw new RenameRuleException("자릿수는 0~10 사이여야 합니다.");
        if (start < 0)
            throw new RenameRuleException("시작 번호는 0 이상이어야 합니다.");
        string separator = baseName.Length == 0 ? "" : sep switch
        {
            SequenceSeparator.Space => " ",
            SequenceSeparator.Underscore => "_",
            SequenceSeparator.Hyphen => "-",
            _ => "",
        };
        int width = Math.Max(1, digits);
        var result = new List<string>(stems.Count);
        for (int i = 0; i < stems.Count; i++)
            result.Add(baseName + separator + (start + i).ToString().PadLeft(width, '0'));
        return result;
    }
}
