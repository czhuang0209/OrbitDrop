using OrbitDrop.Core;

namespace OrbitDrop.Tools;

/// <summary>Non-destructive file actions available for any file type. Nothing is moved, renamed or deleted.</summary>
public sealed class FileActionsProvider : IOperationProvider
{
    public const string ProviderId = "file.actions";
    public string Id => ProviderId;

    public IEnumerable<OperationDescriptor> GetOperations(FileSet files, OperationMode mode)
    {
        if (mode != OperationMode.Actions || files.IsEmpty) yield break;
        bool many = files.Count > 1;
        yield return new("action.copy-path", ProviderId, OperationMode.Actions, many ? "Copy paths" : "Copy path", "",
            "%1", many ? "Copy paths" : "Copy path", "Copying path", 200);
        yield return new("action.copy-name", ProviderId, OperationMode.Actions, many ? "Copy names" : "Copy name", "",
            "%1", many ? "Copy names" : "Copy name", "Copying name", 201);
        yield return new("action.reveal", ProviderId, OperationMode.Actions, "Show in folder", "",
            "%1", "Show in folder", "Opening folder", 202);
    }

    public Task ExecuteAsync(OperationDescriptor operation, FileSet files, OperationContext context,
        IProgress<FileOutcome> progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        switch (operation.Id)
        {
            case "action.copy-path":
                context.Shell.SetClipboardText(string.Join(Environment.NewLine, files.Select(f => f.Contains(' ') ? $"\"{f}\"" : f)));
                break;
            case "action.copy-name":
                context.Shell.SetClipboardText(string.Join(Environment.NewLine, files.Select(Path.GetFileName)));
                break;
            case "action.reveal":
                context.Shell.Reveal(files);
                break;
            default:
                throw new NotSupportedException(operation.Id);
        }
        foreach (var f in files) progress.Report(new FileOutcome(f, OutcomeStatus.Succeeded));
        return Task.CompletedTask;
    }

    public IEnumerable<(string Category, string Detail)> DescribeCapabilities()
    {
        yield return ("File actions", "Copy path, Copy name, Show in folder (any file)");
    }
}
