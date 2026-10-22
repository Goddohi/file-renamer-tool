using System.Text.Json;

namespace FileRenamer.Core;

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileRenamer", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(path)) return new AppSettings();
            return Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options));
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    private static AppSettings Normalize(AppSettings? s)
    {
        var d = new AppSettings();
        if (s == null) return d;
        return s with
        {
            DateFormat = string.IsNullOrEmpty(s.DateFormat) ? d.DateFormat : s.DateFormat,
            CompanyName = s.CompanyName ?? d.CompanyName,
            UserName = s.UserName ?? d.UserName,
            SelectedMode = s.SelectedMode is >= 0 and <= 3 ? s.SelectedMode : d.SelectedMode,
            ReplaceFind = s.ReplaceFind ?? d.ReplaceFind,
            ReplaceWith = s.ReplaceWith ?? d.ReplaceWith,
            Template = s.Template ?? d.Template,
            CaseStyle = Enum.IsDefined(s.CaseStyle) ? s.CaseStyle : d.CaseStyle,
            SequenceBaseName = s.SequenceBaseName ?? d.SequenceBaseName,
            SequenceStart = s.SequenceStart >= 0 ? s.SequenceStart : d.SequenceStart,
            SequenceDigits = s.SequenceDigits is >= 0 and <= 10 ? s.SequenceDigits : d.SequenceDigits,
            SequenceSeparator = Enum.IsDefined(s.SequenceSeparator) ? s.SequenceSeparator : d.SequenceSeparator,
        };
    }

    public void Save(AppSettings s)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(s, Options));
    }
}
