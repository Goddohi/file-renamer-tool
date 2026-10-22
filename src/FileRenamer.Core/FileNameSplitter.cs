namespace FileRenamer.Core;

public static class FileNameSplitter
{
    /// <summary>Splits at the last '.'; ext includes the dot. ".gitignore" -> (".gitignore", "").</summary>
    public static (string Stem, string Ext) Split(string fileName)
    {
        int i = fileName.LastIndexOf('.');
        if (i <= 0) return (fileName, "");
        return (fileName[..i], fileName[i..]);
    }
}
