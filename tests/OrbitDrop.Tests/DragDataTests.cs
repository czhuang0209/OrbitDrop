using System.IO;
using System.Text;
using OrbitDrop.Windows.DragDrop;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace OrbitDrop.Tests;

/// <summary>Reading the dragged files from apps that don't provide CF_HDROP (VS Code's Explorer and other Chromium apps).</summary>
public class DragDataTests
{
    /// <summary>Chromium's custom web data, the way Chromium writes it (base::Pickle).</summary>
    private static byte[] Pickle(params (string Type, string Value)[] entries)
    {
        var body = new MemoryStream();
        var w = new BinaryWriter(body);
        w.Write((uint)entries.Length);
        void Str(string s)
        {
            w.Write(s.Length);
            w.Write(Encoding.Unicode.GetBytes(s));
            int pad = (4 - s.Length * 2 % 4) % 4;
            for (int i = 0; i < pad; i++) w.Write((byte)0);
        }
        foreach (var (t, v) in entries) { Str(t); Str(v); }
        w.Flush();
        var all = new MemoryStream();
        new BinaryWriter(all).Write((uint)body.Length);
        body.WriteTo(all);
        return all.ToArray();
    }

    private static string TempFile(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), "orbitdrop-dragtests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void Parses_chromium_custom_data()
    {
        var parsed = DataObjectReader.ParseChromiumCustomData(Pickle(("codefiles", "[\"C:\\\\a.png\"]"), ("odd", "x")));
        Assert.Equal(2, parsed.Count);
        Assert.Equal(("codefiles", "[\"C:\\\\a.png\"]"), parsed[0]);
        Assert.Equal(("odd", "x"), parsed[1]);
    }

    [Fact]
    public void Truncated_custom_data_is_ignored_safely()
    {
        var bytes = Pickle(("codefiles", "[\"C:\\\\a.png\"]"));
        Assert.Empty(DataObjectReader.ParseChromiumCustomData(bytes[..10]));
        Assert.Empty(DataObjectReader.ParseChromiumCustomData([]));
    }

    [Fact]
    public void File_uris_become_paths()
    {
        var paths = DataObjectReader.PathsFromUris(["file:///c%3A/Users/me/my%20photo.png", "# comment", "https://example.com/x.png", ""]);
        Assert.Equal([@"c:\Users\me\my photo.png"], paths);
    }

    [Fact]
    public void Text_is_a_file_list_only_when_every_line_is_an_existing_full_path()
    {
        Func<string, bool> exists = p => p is @"C:\a.png" or @"C:\b.png";
        Assert.Equal([@"C:\a.png", @"C:\b.png"], DataObjectReader.PathsFromText("C:\\a.png\r\nC:\\b.png\r\n", exists));
        Assert.Empty(DataObjectReader.PathsFromText("C:\\a.png\r\nhello", exists));
        Assert.Empty(DataObjectReader.PathsFromText("a.png", _ => true)); // relative
        Assert.Empty(DataObjectReader.PathsFromText("", exists));
    }

    [Fact]
    public void Reads_vs_code_style_drag_without_cf_hdrop()
    {
        string a = TempFile("photo.png"), b = TempFile("notes.txt");
        string json = System.Text.Json.JsonSerializer.Serialize(new[] { a, b });
        var data = new System.Windows.DataObject();
        data.SetData("Chromium Web Custom MIME Data Format", new MemoryStream(Pickle(("codefiles", json), ("resourceurls", "[]"))));
        data.SetData(System.Windows.DataFormats.UnicodeText, "not a path");
        Assert.Equal([a, b], DataObjectReader.ReadFileDrop((ComIDataObject)data));
    }

    [Fact]
    public void Falls_back_to_paths_in_plain_text()
    {
        string a = TempFile("clip.mp4");
        var data = new System.Windows.DataObject();
        data.SetData(System.Windows.DataFormats.UnicodeText, a + "\r\n");
        Assert.Equal([a], DataObjectReader.ReadFileDrop((ComIDataObject)data));
    }

    [Fact]
    public void Plain_text_that_is_not_a_path_gives_no_files()
    {
        var data = new System.Windows.DataObject();
        data.SetData(System.Windows.DataFormats.UnicodeText, "const x = 1;");
        Assert.Empty(DataObjectReader.ReadFileDrop((ComIDataObject)data));
    }

    [Fact]
    public void Explorer_file_drop_still_wins()
    {
        string a = TempFile("a.jpg");
        var data = new System.Windows.DataObject();
        data.SetData(System.Windows.DataFormats.FileDrop, new[] { a });
        Assert.Equal([a], DataObjectReader.ReadFileDrop((ComIDataObject)data));
    }
}
