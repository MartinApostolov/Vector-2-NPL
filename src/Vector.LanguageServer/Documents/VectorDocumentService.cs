namespace Vector.LanguageServer.Documents;

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Vector.Analysis.Documents;
using Vector.Analysis.Workspace;
using Vector.Analysis.Modules;
using Vector.Core.Diagnostics;
using CoreDiagnosticSeverity = Vector.Core.Diagnostics.DiagnosticSeverity;
using LspDiagnostic = Microsoft.VisualStudio.LanguageServer.Protocol.Diagnostic;
using LspDiagnosticSeverity = Microsoft.VisualStudio.LanguageServer.Protocol.DiagnosticSeverity;
using LspRange = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

internal sealed class VectorDocumentService
{
    private static readonly TimeSpan ChangeDelay = TimeSpan.FromMilliseconds(75);
    private readonly object gate = new();
    private readonly Dictionary<Uri, CancellationTokenSource> pendingChanges = new();
    private readonly VectorWorkspace workspace = new();
    private readonly ILanguageClient client;
    private readonly VectorLanguageServerOptions options;

    public VectorDocumentService(ILanguageClient client, VectorLanguageServerOptions options)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool TryGetDocument(Uri uri, out VectorAnalysisResult? result) =>
        this.workspace.TryGetDocument(uri, out result);

    public VectorModuleIndex Modules => this.workspace.Modules;

    public async Task OpenAsync(TextDocumentItem document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        this.CancelPending(document.Uri);
        VectorDocumentSnapshot snapshot = this.CreateSnapshot(document.Uri, document.Text, document.Version);
        if (this.workspace.TryUpdateDocument(snapshot, out VectorAnalysisResult result, cancellationToken))
        {
            await this.PublishAsync(result).ConfigureAwait(false);
        }
    }

    public async Task ChangeAsync(
        VersionedTextDocumentIdentifier document,
        TextDocumentContentChangeEvent[] contentChanges,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(contentChanges);
        if (contentChanges.Length == 0)
        {
            throw new ArgumentException("A full document change must contain source text.", nameof(contentChanges));
        }

        var pending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (this.gate)
        {
            if (this.pendingChanges.Remove(document.Uri, out CancellationTokenSource? previous))
            {
                previous.Cancel();
                previous.Dispose();
            }

            this.pendingChanges.Add(document.Uri, pending);
        }

        try
        {
            await Task.Delay(ChangeDelay, pending.Token).ConfigureAwait(false);
            VectorDocumentSnapshot snapshot = this.CreateSnapshot(document.Uri, contentChanges[^1].Text, document.Version);
            if (this.workspace.TryUpdateDocument(snapshot, out VectorAnalysisResult result, pending.Token))
            {
                await this.PublishAsync(result).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (pending.IsCancellationRequested)
        {
        }
        finally
        {
            lock (this.gate)
            {
                if (this.pendingChanges.TryGetValue(document.Uri, out CancellationTokenSource? current)
                    && ReferenceEquals(current, pending))
                {
                    this.pendingChanges.Remove(document.Uri);
                }
            }

            pending.Dispose();
        }
    }

    public async Task CloseAsync(TextDocumentIdentifier document)
    {
        ArgumentNullException.ThrowIfNull(document);
        this.CancelPending(document.Uri);
        this.workspace.RemoveDocument(document.Uri);
        await this.client.PublishDiagnosticsAsync(new PublishDiagnosticParams
        {
            Uri = document.Uri,
            Diagnostics = [],
        }).ConfigureAwait(false);
    }

    private VectorDocumentSnapshot CreateSnapshot(Uri uri, string text, int version)
    {
        if (uri.IsFile)
        {
            string? programRoot = this.options.ProgramRoot
                ?? this.workspace.Modules.GetInheritedProgramRoot(uri.LocalPath);
            return VectorDocumentSnapshot.FromFile(uri.LocalPath, text, version, programRoot);
        }

        return new VectorDocumentSnapshot(uri, uri.ToString(), text, version);
    }

    private async Task PublishAsync(VectorAnalysisResult result)
    {
        LspDiagnostic[] diagnostics = this.options.LiveDiagnostics
            ? result.Diagnostics.Select(diagnostic => new LspDiagnostic
        {
            Code = diagnostic.Code.ToString(),
            Message = diagnostic.Message,
            Severity = diagnostic.Severity switch
            {
                CoreDiagnosticSeverity.Error => LspDiagnosticSeverity.Error,
                CoreDiagnosticSeverity.Warning => LspDiagnosticSeverity.Warning,
                _ => LspDiagnosticSeverity.Information,
            },
            Source = "Vector",
            Range = new LspRange
            {
                Start = ToLspPosition(result.Document.LineMap.GetPosition(diagnostic.Span.Start.Offset)),
                End = ToLspPosition(result.Document.LineMap.GetPosition(diagnostic.Span.End.Offset)),
            },
        }).ToArray()
            : [];

        await this.client.PublishDiagnosticsAsync(new PublishDiagnosticParams
        {
            Uri = result.Document.Uri,
            Diagnostics = diagnostics,
        }).ConfigureAwait(false);
    }

    private static Position ToLspPosition(Analysis.Text.TextPosition position) => new()
    {
        Line = position.Line,
        Character = position.Character,
    };

    private void CancelPending(Uri uri)
    {
        lock (this.gate)
        {
            if (this.pendingChanges.Remove(uri, out CancellationTokenSource? pending))
            {
                pending.Cancel();
                pending.Dispose();
            }
        }
    }
}
