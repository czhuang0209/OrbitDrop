using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OrbitDrop.Core;

namespace OrbitDrop.UI.Settings;

/// <summary>
/// Apps people like to open their files in, found on this PC (Settings › Your actions › Add): pick one and the action is
/// made for you ("Open in Photoshop", for photos), with no command or arguments to type. Found through Windows' list of
/// installed programs (App Paths), their usual install folders and the PATH.
/// </summary>
public static class AppCatalog
{
    /// <summary>An app on this PC: its name, program, the files it's for, and how it takes them.</summary>
    public sealed record FoundApp(string Name, string Program, string Kind, string Extensions, bool OneAtATime)
    {
        /// <summary>The action that opens the dropped files in this app.</summary>
        public CustomAction ToAction() => new()
        {
            Name = $"Open in {Name}", Program = Program, Arguments = OneAtATime ? "{file}" : "{files}",
            Kind = Kind, Extensions = Extensions, PerFile = OneAtATime,
        };
    }

    private sealed record Known(string Name, string Kind, string Extensions, bool OneAtATime, string[] AppPaths, string[] Folders);

    private static readonly string Pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    private static readonly string Pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    private static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    // Folders may end in a wildcard part ("Adobe Photoshop*"): the newest match wins.
    private static readonly Known[] Apps =
    [
        new("VS Code", "Everything", "", false, ["Code.exe"], [$@"{Local}\Programs\Microsoft VS Code\Code.exe", $@"{Pf}\Microsoft VS Code\Code.exe"]),
        new("Cursor", "Everything", "", false, ["Cursor.exe"], [$@"{Local}\Programs\cursor\Cursor.exe"]),
        new("Photoshop", "Photos", "", false, ["Photoshop.exe"], [$@"{Pf}\Adobe\Adobe Photoshop*\Photoshop.exe"]),
        new("Illustrator", "Chosen types", ".ai .svg .eps .pdf", false, ["Illustrator.exe"], [$@"{Pf}\Adobe\Adobe Illustrator*\Support Files\Contents\Windows\Illustrator.exe"]),
        new("Premiere Pro", "Videos", "", false, ["Adobe Premiere Pro.exe"], [$@"{Pf}\Adobe\Adobe Premiere Pro*\Adobe Premiere Pro.exe"]),
        new("GIMP", "Photos", "", false, ["gimp-3.exe", "gimp-2.10.exe"], [$@"{Pf}\GIMP 3\bin\gimp-3.exe", $@"{Pf}\GIMP 2\bin\gimp-2.10.exe"]),
        new("Krita", "Photos", "", false, ["krita.exe"], [$@"{Pf}\Krita (x64)\bin\krita.exe"]),
        new("Paint.NET", "Photos", "", false, ["paintdotnet.exe"], [$@"{Pf}\paint.net\paintdotnet.exe"]),
        new("Paint", "Photos", "", true, ["mspaint.exe"], [$@"{Environment.SystemDirectory}\mspaint.exe", $@"{Local}\Microsoft\WindowsApps\mspaint.exe"]),
        new("Word", "Documents", "", false, ["WINWORD.EXE"], []),
        new("Excel", "Chosen types", ".xlsx .xls .csv", false, ["EXCEL.EXE"], []),
        new("PowerPoint", "Chosen types", ".pptx .ppt", false, ["POWERPNT.EXE"], []),
        new("Notepad++", "Everything", "", false, ["notepad++.exe"], [$@"{Pf}\Notepad++\notepad++.exe", $@"{Pf86}\Notepad++\notepad++.exe"]),
        new("Sublime Text", "Everything", "", false, ["sublime_text.exe"], [$@"{Pf}\Sublime Text\sublime_text.exe", $@"{Pf}\Sublime Text 3\sublime_text.exe"]),
        new("Notepad", "Chosen types", ".txt .md .csv .json .log .xml .ini", true, ["notepad.exe"], [$@"{Environment.SystemDirectory}\notepad.exe"]),
        new("VLC", "Videos", "", false, ["vlc.exe"], [$@"{Pf}\VideoLAN\VLC\vlc.exe", $@"{Pf86}\VideoLAN\VLC\vlc.exe"]),
        new("Audacity", "Audio", "", false, ["Audacity.exe"], [$@"{Pf}\Audacity\Audacity.exe"]),
        new("Blender", "Chosen types", ".blend .obj .fbx .stl .glb", true, ["blender.exe"], [$@"{Pf}\Blender Foundation\Blender*\blender.exe"]),
    ];

    /// <summary>The catalogue's apps that are on this PC, in the catalogue's order (looked up once, then remembered).</summary>
    public static IReadOnlyList<FoundApp> Installed => _installed ??= Apps.Select(Find).OfType<FoundApp>().ToList();
    private static IReadOnlyList<FoundApp>? _installed;

    private static FoundApp? Find(Known app)
    {
        string? program = app.AppPaths.Select(FromAppPaths).FirstOrDefault(p => p is not null) ?? app.Folders.Select(FromFolder).FirstOrDefault(p => p is not null);
        return program is null ? null : new FoundApp(app.Name, program, app.Kind, app.Extensions, app.OneAtATime);
    }

    /// <summary>Windows' own list of installed programs (App Paths), for this user and for everyone.</summary>
    private static string? FromAppPaths(string exe)
    {
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = root.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{exe}");
                if (key?.GetValue(null) is string path && File.Exists(path = Environment.ExpandEnvironmentVariables(path.Trim('"')))) return path;
            }
            catch (Exception) { } // a key we may not read
        }
        return null;
    }

    /// <summary>A usual install path; a "*" in a folder name matches any version, the newest first.</summary>
    private static string? FromFolder(string pattern)
    {
        try
        {
            if (!pattern.Contains('*')) return File.Exists(pattern) ? pattern : null;
            int star = pattern.IndexOf('*');
            int folderEnd = pattern.IndexOf('\\', star);
            string parent = Path.GetDirectoryName(pattern[..star])!, mask = Path.GetFileName(pattern[..folderEnd]), rest = pattern[(folderEnd + 1)..];
            if (!Directory.Exists(parent)) return null;
            return Directory.EnumerateDirectories(parent, mask).OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                .Select(d => Path.Combine(d, rest)).FirstOrDefault(File.Exists);
        }
        catch (Exception) { return null; }
    }

    /// <summary>A name for any program the user browses to: its own description ("Blender"), or its file name.</summary>
    public static string NameOf(string program)
    {
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(program);
            string name = (info.FileDescription ?? info.ProductName ?? "").Trim();
            if (name.Length is > 0 and < 40) return name;
        }
        catch (Exception) { }
        return Path.GetFileNameWithoutExtension(program);
    }

    private static readonly Dictionary<string, ImageSource?> Icons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The program's own icon (32 px), or null when it has none to give (Store apps' shortcuts, commands).</summary>
    public static ImageSource? IconOf(string? program)
    {
        if (string.IsNullOrWhiteSpace(program) || !Path.IsPathRooted(program) || !File.Exists(program)) return null;
        if (Icons.TryGetValue(program, out var known)) return known;
        ImageSource? icon = null;
        try
        {
            if (ExtractIconEx(program, 0, out IntPtr large, out IntPtr small, 1) > 0 && large != IntPtr.Zero)
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(large, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                source.Freeze();
                icon = source;
                DestroyIcon(large);
                if (small != IntPtr.Zero) DestroyIcon(small);
            }
        }
        catch (Exception) { }
        return Icons[program] = icon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint icons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);
}
