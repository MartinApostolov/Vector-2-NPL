namespace Vector.LanguageServer;

using Microsoft.VisualStudio.LanguageServer.Protocol;

public interface ILanguageClient
{
    Task PublishDiagnosticsAsync(PublishDiagnosticParams parameters);
}
