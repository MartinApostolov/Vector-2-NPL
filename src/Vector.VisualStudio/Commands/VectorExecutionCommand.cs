namespace Vector.VisualStudio.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Editor;
using Vector.ExecutionProtocol;
using Vector.VisualStudio.Execution;
using Vector.VisualStudio.Output;

public abstract class VectorExecutionCommand : Command
{
    private readonly VectorExecutionClient client;
    private readonly VectorOutputService output;

    protected VectorExecutionCommand(VectorExecutionClient client, VectorOutputService output)
    {
        this.client = client;
        this.output = output;
    }

    protected abstract VectorExecutionEngine Engine { get; }

    protected virtual VectorExecutionOperation Operation => VectorExecutionOperation.Run;

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        using ITextViewSnapshot? textView = await context.GetActiveTextViewAsync(cancellationToken);
        if (textView is null)
        {
            await this.output.WriteAsync(this.Extensibility, "Vector: no active text document.", cancellationToken);
            return;
        }

        Uri uri = textView.Document.Uri;
        string? sourcePath = uri.IsFile ? uri.LocalPath : null;
        var request = new VectorExecutionRequest
        {
            Source = textView.Document.Text.CopyToString(),
            SourcePath = sourcePath,
            ProgramRoot = sourcePath is null ? null : Path.GetDirectoryName(sourcePath),
            Engine = this.Engine,
            Operation = this.Operation,
        };

        VectorExecutionResponse response = await this.client.ExecuteAsync(request, cancellationToken);
        await this.output.WriteAsync(
            this.Extensibility,
            VectorExecutionOutputFormatter.Format(request, response),
            cancellationToken);
    }

}
