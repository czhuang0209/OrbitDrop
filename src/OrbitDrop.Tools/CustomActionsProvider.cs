using System.ComponentModel;
using System.Diagnostics;
using OrbitDrop.Core;

namespace OrbitDrop.Tools;

/// <summary>
/// The user's own Actions (Settings → Your actions) in Actions › Your actions: each runs a program or a command with
/// the dropped files. The first <see cref="CustomAction.FreeActions"/> are free; the rest are part of the upgrade.
/// </summary>
public sealed class CustomActionsProvider(Func<IReadOnlyList<CustomAction>> actions) : IOperationProvider
{
    public const string ProviderId = "custom.actions";
    public const string Prefix = "action.custom.";
    public static readonly OperationGroup Group = new("actions.custom", "Your actions", "");
    public string Id => ProviderId;

    /// <summary>How long a hidden run is watched for an immediate failure ("not recognized", a missing file).</summary>
    private static readonly TimeSpan Watch = TimeSpan.FromSeconds(1.5);

    public IEnumerable<OperationDescriptor> GetOperations(FileSet files, OperationMode mode)
    {
        if (mode != OperationMode.Actions || files.IsEmpty) yield break;
        var list = actions();
        for (int i = 0; i < list.Count; i++)
        {
            var action = list[i];
            if (action.Problem() is not null || !files.All(action.Accepts)) continue;
            yield return new($"{Prefix}{i}", ProviderId, OperationMode.Actions, action.Name, "", "%1", action.Name,
                $"Running {action.Name}", 250 + i, Group: Group, DoneTitle: $"{action.Name}: done", RequiresLicense: i >= CustomAction.FreeActions);
        }
    }

    public async Task ExecuteAsync(OperationDescriptor operation, FileSet files, OperationContext context,
        IProgress<FileOutcome> progress, CancellationToken ct)
    {
        var list = actions();
        int index = int.Parse(operation.Id[Prefix.Length..]);
        if (index >= list.Count)
        {
            foreach (var f in files) progress.Report(new FileOutcome(f, OutcomeStatus.Failed, Message: "That action was removed."));
            return;
        }
        var action = list[index];
        IEnumerable<IReadOnlyList<string>> runs = action.PerFile ? files.Select(f => (IReadOnlyList<string>)[f]) : [files.ToList()];
        foreach (var run in runs)
        {
            ct.ThrowIfCancellationRequested();
            string? problem;
            try
            {
                int? exit = await LaunchAsync(action, run, ct);
                problem = exit is int code && code != 0
                    ? $"“{action.Name}” didn't work: it ended with code {code}. Check it in Settings › Your actions (turn on “Show its window” to see why)."
                    : null;
            }
            catch (Win32Exception)
            {
                problem = $"Windows couldn't start “{action.Program}”. Check the program in Settings › Your actions.";
            }
            foreach (var f in run)
                progress.Report(problem is null ? new FileOutcome(f, OutcomeStatus.Succeeded) : new FileOutcome(f, OutcomeStatus.Failed, Message: problem));
        }
    }

    /// <summary>Starts the action for <paramref name="files"/>; returns its exit code when a hidden run ends quickly, else null.</summary>
    private static async Task<int?> LaunchAsync(CustomAction action, IReadOnlyList<string> files, CancellationToken ct)
    {
        var (fileName, arguments) = CommandLine(action, action.Expand(files));
        string folder = Path.GetDirectoryName(files[0].TrimEnd('\\')) ?? "";
        var info = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false, CreateNoWindow = !action.ShowWindow,
            WorkingDirectory = Directory.Exists(folder) ? folder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };
        using var process = Process.Start(info) ?? throw new Win32Exception();
        if (action.ShowWindow) return null; // its window stays open for the user; nothing to wait for
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Watch);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; } // still running: a program that opened
        return process.ExitCode;
    }

    private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    /// <summary>
    /// What to start: a program found as an .exe runs directly (no command prompt in between); scripts on the PATH
    /// (VS Code's "code" is code.cmd) and plain commands go through cmd. "Show its window" keeps cmd's window open
    /// when it's done (/k), so a script's output can be read.
    /// </summary>
    public static (string FileName, string Arguments) CommandLine(CustomAction action, string expanded)
    {
        string program = action.Program.Trim().Trim('"');
        string? found = program.Length == 0 ? null : Resolve(program);
        bool direct = found is not null && (found.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || found.EndsWith(".com", StringComparison.OrdinalIgnoreCase));
        if (direct && !action.ShowWindow) return (found!, expanded);
        string line = program.Length == 0 ? expanded : $"{CustomAction.Quote(found ?? program)} {expanded}".TrimEnd();
        return (Cmd, $"/d /s /{(action.ShowWindow ? "k" : "c")} \"{line}\"");
    }

    /// <summary>A program's full path: as given, or found on the PATH (with .exe, .com, .cmd or .bat); null if it isn't there.</summary>
    public static string? Resolve(string program)
    {
        if (Path.IsPathRooted(program)) return File.Exists(program) ? program : null;
        string[] extensions = Path.HasExtension(program) ? [""] : [".exe", ".com", ".cmd", ".bat"];
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            foreach (string ext in extensions)
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim().Trim('"'), program + ext);
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException) { } // a malformed PATH entry
            }
        return null;
    }

    public IEnumerable<(string Category, string Detail)> DescribeCapabilities()
    {
        yield return ("Your actions", $"Your own programs and commands on the wheel, with the dropped files ({CustomAction.FreeActions} free, more with the upgrade)");
    }
}
