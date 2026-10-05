using System.IO;

namespace EvaGest.Services;

/// <summary>
/// One of the files an export would replace is held open by another program — in
/// practice the spreadsheet the accountant's previous export is open in. Thrown before
/// any file is written, so the folder is left exactly as it was. Its own type so the
/// dialog can name the file and say to close it, instead of the generic error.
/// </summary>
public class ExportFileInUseException(string path, Exception inner)
    : IOException($"The export file {path} is open in another program.", inner)
{
    /// <summary>The file name alone, which is what the user recognises.</summary>
    public string FileName { get; } = Path.GetFileName(path);
}
