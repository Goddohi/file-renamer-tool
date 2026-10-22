using System.Globalization;
using System.Text.RegularExpressions;

namespace FileRenamer.Core;

public sealed class TemplateMode(string template, AppSettings settings, DateTime today) : IRenameMode
{
    private static readonly Regex VariablePattern = new(@"\{([^{}]*)\}", RegexOptions.Compiled);

    public IReadOnlyList<string> Apply(IReadOnlyList<string> stems)
    {
        string date;
        try
        {
            date = today.ToString(settings.DateFormat, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            throw new RenameRuleException("날짜 형식이 올바르지 않습니다.");
        }

        // Validate first so an unknown variable is reported even when the list is empty.
        foreach (Match m in VariablePattern.Matches(template))
        {
            if (m.Groups[1].Value is not ("날짜" or "회사명" or "파일명" or "이름"))
                throw new RenameRuleException($"알 수 없는 변수입니다: {m.Value}");
        }

        return stems.Select(stem => VariablePattern.Replace(template, m => m.Groups[1].Value switch
        {
            "날짜" => date,
            "회사명" => settings.CompanyName,
            "이름" => settings.UserName,
            _ => stem, // 파일명
        })).ToList();
    }
}
