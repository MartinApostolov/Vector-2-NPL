namespace Vector.VisualStudio.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Editor;
using Vector.ExecutionProtocol;
using Vector.VisualStudio.Execution;
using Vector.VisualStudio.Output;
using Vector.VisualStudio.Settings;

internal abstract class VectorExecutionCommand : Command
{
    private readonly VectorExecutionClient client;
    private readonly VectorOutputService output;
    private readonly VectorSettingsService settings;

    protected VectorExecutionCommand(
        VectorExecutionClient client,
        VectorOutputService output,
        VectorSettingsService settings)
    {
        this.client = client;
        this.output = output;
        this.settings = settings;
    }

    protected virtual VectorExecutionEngine? EngineOverride => null;

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
        VectorIdeSettings currentSettings = await this.settings.GetAsync(cancellationToken);
        VectorExecutionPreparation preparation = VectorExecutionRequestFactory.Create(
            textView.Document.Text.CopyToString(),
            sourcePath,
            currentSettings,
            this.EngineOverride,
            this.Operation);
        if (preparation.Failure is not null)
        {
            await this.output.WriteAsync(
                this.Extensibility,
                $"Vector execution settings error [{preparation.Failure.Code}]: {preparation.Failure.Message}",
                cancellationToken);
            return;
        }

        VectorExecutionRequest request = preparation.Request!;
        VectorExecutionResponse response = await this.client.ExecuteAsync(request, cancellationToken);
        await this.output.WriteAsync(
            this.Extensibility,
            VectorExecutionOutputFormatter.Format(request, response),
            cancellationToken);
    }

}
