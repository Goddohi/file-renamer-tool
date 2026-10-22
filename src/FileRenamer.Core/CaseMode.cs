using System.Text;

namespace FileRenamer.Core;

public sealed class CaseMode(CaseStyle style) : IRenameMode
{
    public IReadOnlyList<string> Apply(IReadOnlyList<string> stems) => stems.Select(Convert).ToList();

    private string Convert(string s) => style switch
    {
        CaseStyle.Lower => s.ToLowerInvariant(),
        CaseStyle.Upper => s.ToUpperInvariant(),
        CaseStyle.FirstUpper => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant(),
        CaseStyle.TitleCase => Title(s),
        _ => s,
    };

    private static string Title(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool startOfWord = true;
        foreach (char c in s)
        {
            if (c is ' ' or '_' or '-')
            {
                sb.Append(c);
                startOfWord = true;
            }
            else
            {
                sb.Append(startOfWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                startOfWord = false;
            }
        }
        return sb.ToString();
    }
}
