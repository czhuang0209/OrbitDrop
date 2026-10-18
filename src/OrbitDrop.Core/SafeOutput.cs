using System.Security.Cryptography;

namespace OrbitDrop.Core;

/// <summary>
/// File-integrity guarantees for every output OrbitDrop writes:
/// <list type="bullet">
/// <item>the original is never opened for writing;</item>
/// <item>output is written to a hidden temp file in the destination folder, then validated;</item>
/// <item>it is published with an atomic rename that never replaces an existing file (collision → " (2)", " (3)" …);</item>
/// <item>on any failure or cancellation the temp file is deleted, so no partial output is ever visible.</item>
/// </list>
/// </summary>
public static class SafeOutput
{
    public const string TempPrefix = ".orbitdrop-";
    private const int MaxAttempts = 999;

    /// <param name="newExtension">Extension including the dot; null keeps the source extension.</param>
    /// <param name="suffix">Tool suffix: "rotated" → "photo (rotated).jpg".</param>
    /// <param name="validate">Returns null if the temp file is valid, otherwise a reason.</param>
    public static string Write(string source, OutputOptions options, string? newExtension, string? suffix,
        Action<Stream> write, Func<string, string?> validate, CancellationToken cancellationToken = default) =>
        WriteToPath(source, options, newExtension, suffix, temp =>
        {
            using var stream = new FileStream(temp, FileMode.Open, FileAccess.Write, FileShare.None);
            write(stream);
        }, validate, cancellationToken);

    /// <summary>
    /// Same guarantees as <see cref="Write"/>, for engines that must open the output themselves (e.g. Media Foundation).
    /// <paramref name="writeToTemp"/> receives the path of an existing, empty temp file to fill. Set <paramref name="hideWhileWriting"/>
    /// to false for Windows storage APIs, which refuse to open hidden files; the file is hidden as soon as it is written.
    /// </summary>
    public static string WriteToPath(string source, OutputOptions options, string? newExtension, string? suffix,
        Action<string> writeToTemp, Func<string, string?> validate, CancellationToken cancellationToken = default,
        bool hideWhileWriting = true)
    {
        string dir = options.DirectoryFor(source);
        Directory.CreateDirectory(dir);
        string ext = newExtension ?? Path.GetExtension(source);
        string baseName = Path.GetFileNameWithoutExtension(source) + (suffix is null ? "" : $" ({suffix})");
        string temp = Path.Combine(dir, $"{TempPrefix}{Guid.NewGuid():N}.tmp");

        try
        {
            using (new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { } // reserve the name
            if (hideWhileWriting) File.SetAttributes(temp, FileAttributes.Hidden);
            writeToTemp(temp);
            if (!hideWhileWriting && File.Exists(temp)) File.SetAttributes(temp, FileAttributes.Hidden);
            cancellationToken.ThrowIfCancellationRequested();

            if (new FileInfo(temp).Length == 0) throw new IOException("The output was empty.");
            if (validate(temp) is { } problem) throw new IOException($"The output failed validation: {problem}");
            cancellationToken.ThrowIfCancellationRequested();

            for (int n = 1; n <= MaxAttempts; n++)
            {
                string candidate = Path.Combine(dir, n == 1 ? $"{baseName}{ext}" : $"{baseName} ({n}){ext}");
                if (File.Exists(candidate) || Directory.Exists(candidate)) continue;
                try
                {
                    File.Move(temp, candidate, overwrite: false); // MoveFileEx without REPLACE_EXISTING
                }
                catch (IOException) when (File.Exists(candidate))
                {
                    continue; // another writer won the race for this name
                }
                File.SetAttributes(candidate, File.GetAttributes(candidate) & ~FileAttributes.Hidden);
                return candidate;
            }
            throw new IOException("No free output file name was found.");
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { /* best effort */ }
        }
    }

    public static string Sha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static IEnumerable<string> LeftoverTemps(string directory) =>
        Directory.EnumerateFiles(directory, $"{TempPrefix}*.tmp");
}
