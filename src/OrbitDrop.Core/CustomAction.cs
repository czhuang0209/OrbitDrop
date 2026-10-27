namespace OrbitDrop.Core;

/// <summary>
/// One of the user's own Actions (Settings → Your actions): runs a program or a command with the dropped files, e.g.
/// "Open in VS Code": <c>code {files}</c>. The first <see cref="FreeActions"/> work for everyone; more are part of the
/// upgrade. Only actions the user made run: nothing is ever imported.
/// </summary>
public sealed class CustomAction
{
    public const int FreeActions = 2;
    public const int MaxActions = 12;

    /// <summary>Which files an action is offered for.</summary>
    public static readonly string[] Kinds = ["Everything", "Photos", "Videos", "Audio", "PDFs", "Documents", "Chosen types"];

    private static readonly Dictionary<string, string[]> KindExtensions = new(StringComparer.Ordinal)
    {
        ["Photos"] = [".jpg", ".jpeg", ".jfif", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".heic", ".heif", ".avif", ".svg", ".ico", ".jxl"],
        ["Videos"] = [".mp4", ".mov", ".mkv", ".avi", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp"],
        ["Audio"] = [".mp3", ".wav", ".flac", ".m4a", ".aac", ".wma", ".ogg", ".opus"],
        ["PDFs"] = [".pdf"],
        ["Documents"] = [".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp", ".rtf", ".txt", ".md", ".csv"],
    };

    public string Name { get; set; } = "";
    /// <summary>A program (a full path, or a name on the PATH like "code" or "python"). Empty: <see cref="Arguments"/> is a command for cmd.</summary>
    public string Program { get; set; } = "";
    /// <summary>What's passed to the program, with {file}, {files}, {folder} (quoted) and {name}, {ext}.</summary>
    public string Arguments { get; set; } = "{file}";
    /// <summary>One of <see cref="Kinds"/>.</summary>
    public string Kind { get; set; } = "Everything";
    /// <summary>For "Chosen types": extensions like ".psd .ai" (dots and commas optional).</summary>
    public string Extensions { get; set; } = "";
    /// <summary>Run once for each file (true), or once with all of them (false).</summary>
    public bool PerFile { get; set; } = true;
    /// <summary>Show the program's console window (a script's output); off runs consoles hidden.</summary>
    public bool ShowWindow { get; set; }

    public CustomAction Clone() => (CustomAction)MemberwiseClone();

    /// <summary>Whether this action is offered for <paramref name="path"/>. Folders only match Everything.</summary>
    public bool Accepts(string path)
    {
        if (Kind == "Everything") return true;
        if (Directory.Exists(path)) return false;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext.Length == 0) return false;
        if (Kind == "Chosen types") return ChosenExtensions().Contains(ext);
        return KindExtensions.TryGetValue(Kind, out var list) && list.Contains(ext);
    }

    /// <summary>The "Chosen types" extensions, normalised to ".psd".</summary>
    public HashSet<string> ChosenExtensions() =>
        Extensions.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(e => "." + e.Trim().TrimStart('*').TrimStart('.').ToLowerInvariant())
            .Where(e => e.Length > 1).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// <see cref="Arguments"/> with the placeholders filled in for <paramref name="files"/> (the first is {file}).
    /// {file}, {files} and {folder} come quoted, so names with spaces, &amp; or apostrophes are passed whole.
    /// </summary>
    public string Expand(IReadOnlyList<string> files)
    {
        string first = files.Count > 0 ? files[0] : "";
        return Arguments
            .Replace("{files}", string.Join(" ", files.Select(Quote)), StringComparison.OrdinalIgnoreCase)
            .Replace("{file}", Quote(first), StringComparison.OrdinalIgnoreCase)
            .Replace("{folder}", Quote(Path.GetDirectoryName(first.TrimEnd('\\')) ?? first), StringComparison.OrdinalIgnoreCase)
            .Replace("{name}", Path.GetFileNameWithoutExtension(first), StringComparison.OrdinalIgnoreCase)
            .Replace("{ext}", Path.GetExtension(first).TrimStart('.'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A path in double quotes for a Windows command line (a trailing backslash is doubled, so it can't escape the quote).</summary>
    public static string Quote(string path) => "\"" + (path.EndsWith('\\') ? path + "\\" : path) + "\"";

    /// <summary>What a problem with this action is, or null when it's ready to run.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name)) return "Give it a name.";
        if (string.IsNullOrWhiteSpace(Program) && string.IsNullOrWhiteSpace(Arguments)) return "Choose a program, or type a command.";
        if (Kind == "Chosen types" && ChosenExtensions().Count == 0) return "Type the file types it's for, like .psd .ai";
        return null;
    }
}
