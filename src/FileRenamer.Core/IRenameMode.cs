namespace FileRenamer.Core;

public interface IRenameMode
{
    /// <summary>stems = file names without extension, in list order. Returns new stems (same count/order).</summary>
    /// <exception cref="RenameRuleException">invalid input (bad regex, unknown variable).</exception>
    IReadOnlyList<string> Apply(IReadOnlyList<string> stems);
}
