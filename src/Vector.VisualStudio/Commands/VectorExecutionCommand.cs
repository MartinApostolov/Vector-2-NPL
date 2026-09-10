namespace Vector.VisualStudio.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Extensibility.Shell;
using Vector.ExecutionProtocol;
using Vector.VisualStudio.Execution;
using Vector.VisualStudio.Output;
using Vector.VisualStudio.Settings;

internal abstract class VectorExecutionCommand : Command
{
    private readonly VectorExecutionClient client;
    private readonly VectorOutputService output;
    private readonly VectorSettingsService settings;
    private Exception? outputInitializationFailure;

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

    public override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Output channels are extension resources. Creating the channel while the
        // command set initializes follows the VS 2026 lifecycle and makes the
        // channel available before the first execution command writes to it.
        try
        {
            await this.output.InitializeAsync(this.Extensibility, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            // Keep the command available so ExecuteCommandAsync can retry and
            // surface the failure through the prompt fallback.
            this.outputInitializationFailure = error;
        }

        await base.InitializeAsync(cancellationToken);
    }

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        try
        {
            await this.ExecuteCoreAsync(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            await this.ReportUnexpectedFailureAsync(error, cancellationToken);
        }
    }

    private async Task ExecuteCoreAsync(IClientContext context, CancellationToken cancellationToken)
    {
        if (this.outputInitializationFailure is not null)
        {
            try
            {
                await this.output.InitializeAsync(this.Extensibility, cancellationToken);
                this.outputInitializationFailure = null;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    "The Vector Output channel could not be initialized.",
                    error);
            }
        }

        using ITextViewSnapshot? textView = await context.GetActiveTextViewAsync(cancellationToken);
        if (textView is null)
        {
            await this.ReportUserErrorAsync("Vector: no active text document.", cancellationToken);
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
            await this.ReportUserErrorAsync(
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
        if (response.HostFailure is not null)
        {
            await this.Extensibility.Shell().ShowPromptAsync(
                $"Vector execution failed [{response.HostFailure.Code}]: {response.HostFailure.Message}\n\nSee the Vector Output channel for details.",
                PromptOptions.OK,
                cancellationToken);
        }
    }

    private async Task ReportUserErrorAsync(string message, CancellationToken cancellationToken)
    {
        await this.output.WriteAsync(this.Extensibility, message, cancellationToken);
        await this.Extensibility.Shell().ShowPromptAsync(message, PromptOptions.OK, cancellationToken);
    }

    private async Task ReportUnexpectedFailureAsync(Exception error, CancellationToken cancellationToken)
    {
        string message = $"Vector command failed [{error.GetType().Name}]: {error.Message}";
        try
        {
            await this.output.WriteAsync(this.Extensibility, message, cancellationToken);
        }
        catch (Exception outputError) when (outputError is not OperationCanceledException)
        {
            message += $"\n\nThe Vector Output channel was unavailable: {outputError.Message}";
        }

        await this.Extensibility.Shell().ShowPromptAsync(message, PromptOptions.OK, cancellationToken);
    }
}
