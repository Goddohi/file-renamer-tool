using System.Text.RegularExpressions;

namespace FileRenamer.Core;

public sealed class ReplaceMode(string find, string replace, bool useRegex) : IRenameMode
{
    public IReadOnlyList<string> Apply(IReadOnlyList<string> stems)
    {
        if (!useRegex)
        {
            if (find.Length == 0) return stems.ToList();
            return stems.Select(s => s.Replace(find, replace, StringComparison.Ordinal)).ToList();
        }

        if (find.Length == 0) return stems.ToList();
        Regex regex;
        try
        {
            regex = new Regex(find, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException ex)
        {
            throw new RenameRuleException("정규식이 올바르지 않습니다: " + ex.Message);
        }

        try
        {
            return stems.Select(s => regex.Replace(s, replace)).ToList();
        }
        catch (RegexMatchTimeoutException)
        {
            throw new RenameRuleException("정규식 처리 시간이 너무 오래 걸립니다.");
        }
        catch (ArgumentException ex)
        {
            throw new RenameRuleException("바꿀 글자가 올바르지 않습니다: " + ex.Message);
        }
    }
}
