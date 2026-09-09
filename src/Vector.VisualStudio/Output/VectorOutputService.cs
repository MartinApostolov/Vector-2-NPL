namespace Vector.VisualStudio.Output;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW
public sealed class VectorOutputService : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private OutputChannel? channel;

    public async Task WriteAsync(
        VisualStudioExtensibility extensibility,
        string text,
        CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            this.channel ??= await extensibility.Views().Output.CreateOutputChannelAsync(
                "Vector",
                cancellationToken);
            await this.channel.WriteLineAsync(text);
        }
        finally
        {
            this.gate.Release();
        }
    }

    public void Dispose()
    {
        this.channel?.Dispose();
        this.gate.Dispose();
    }
}
#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW
