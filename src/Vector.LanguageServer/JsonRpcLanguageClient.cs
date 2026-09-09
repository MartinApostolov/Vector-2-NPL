namespace Vector.LanguageServer;

using Microsoft.VisualStudio.LanguageServer.Protocol;
using StreamJsonRpc;

internal sealed class JsonRpcLanguageClient(JsonRpc rpc) : ILanguageClient
{
    private readonly JsonRpc rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));

    public Task PublishDiagnosticsAsync(PublishDiagnosticParams parameters) =>
        this.rpc.NotifyWithParameterObjectAsync(Methods.TextDocumentPublishDiagnosticsName, parameters);
}
