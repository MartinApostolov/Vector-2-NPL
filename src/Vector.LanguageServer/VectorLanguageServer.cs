namespace Vector.LanguageServer;

using System.Reflection;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using Vector.LanguageServer.Documents;
using Vector.LanguageServer.Protocol;
using Vector.Analysis.Documents;
using Vector.Analysis.IntelliSense;
using Vector.Analysis.Navigation;
using Vector.Analysis.SignatureHelp;

public sealed class VectorLanguageServer
{
    private readonly object stateLock = new();
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly VectorDocumentService documents;
    private readonly VectorCompletionService completionService;
    private readonly VectorHoverService hoverService = new();
    private readonly VectorDefinitionService definitionService;
    private readonly VectorReferenceService referenceService;
    private readonly VectorSignatureHelpService signatureHelpService;
    private ServerLifecycleState state;

    public ServerLifecycleState State
    {
        get
        {
            lock (this.stateLock)
            {
                return this.state;
            }
        }
    }

    public Task Completion => this.completion.Task;

    public int ExitCode { get; private set; }

    public VectorLanguageServer(
        ILanguageClient? client = null,
        VectorLanguageServerOptions? options = null)
    {
        this.documents = new VectorDocumentService(
            client ?? NullLanguageClient.Instance,
            options ?? VectorLanguageServerOptions.Default);
        this.completionService = new VectorCompletionService(this.documents.Modules);
        this.definitionService = new VectorDefinitionService(this.documents.Modules);
        this.referenceService = new VectorReferenceService(this.documents.Modules);
        this.signatureHelpService = new VectorSignatureHelpService(this.documents.Modules);
    }

    [JsonRpcMethod("initialize", UseSingleObjectParameterDeserialization = true)]
    public Task<VectorInitializeResult> InitializeAsync(JToken request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.stateLock)
        {
            if (this.state != ServerLifecycleState.Created)
            {
                throw new InvalidOperationException("The Vector language server can only be initialized once.");
            }

            this.state = ServerLifecycleState.Initialized;
        }

        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        return Task.FromResult(new VectorInitializeResult
        {
            Capabilities = new ServerCapabilities
            {
                TextDocumentSync = new TextDocumentSyncOptions
                {
                    OpenClose = true,
                    Change = TextDocumentSyncKind.Full,
                },
                CompletionProvider = new CompletionOptions { TriggerCharacters = ["."] },
                HoverProvider = true,
                DocumentSymbolProvider = true,
                DefinitionProvider = true,
                ReferencesProvider = true,
                SignatureHelpProvider = new SignatureHelpOptions { TriggerCharacters = ["(", ","] },
            },
            ServerInfo = new VectorServerInfo
            {
                Name = "Vector Language Server",
                Version = version,
            },
        });
    }

    [JsonRpcMethod("initialized", UseSingleObjectParameterDeserialization = true)]
    public Task InitializedAsync(object? request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.RequireState(ServerLifecycleState.Initialized, "initialized");
        return Task.CompletedTask;
    }

    [JsonRpcMethod("shutdown")]
    public Task<object?> ShutdownAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.stateLock)
        {
            if (this.state != ServerLifecycleState.Initialized)
            {
                throw new InvalidOperationException("The Vector language server must be initialized before shutdown.");
            }

            this.state = ServerLifecycleState.Shutdown;
        }

        return Task.FromResult<object?>(null);
    }

    [JsonRpcMethod(Methods.TextDocumentDidOpenName, UseSingleObjectParameterDeserialization = true)]
    public Task DidOpenAsync(DidOpenTextDocumentParams parameters, CancellationToken cancellationToken)
    {
        this.RequireState(ServerLifecycleState.Initialized, Methods.TextDocumentDidOpenName);
        return this.documents.OpenAsync(parameters.TextDocument, cancellationToken);
    }

    [JsonRpcMethod(Methods.TextDocumentDidChangeName, UseSingleObjectParameterDeserialization = true)]
    public Task DidChangeAsync(DidChangeTextDocumentParams parameters, CancellationToken cancellationToken)
    {
        this.RequireState(ServerLifecycleState.Initialized, Methods.TextDocumentDidChangeName);
        return this.documents.ChangeAsync(parameters.TextDocument, parameters.ContentChanges, cancellationToken);
    }

    [JsonRpcMethod(Methods.TextDocumentDidCloseName, UseSingleObjectParameterDeserialization = true)]
    public Task DidCloseAsync(DidCloseTextDocumentParams parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.RequireState(ServerLifecycleState.Initialized, Methods.TextDocumentDidCloseName);
        return this.documents.CloseAsync(parameters.TextDocument);
    }

    [JsonRpcMethod(Methods.TextDocumentCompletionName, UseSingleObjectParameterDeserialization = true)]
    public Task<CompletionItem[]> CompletionAsync(CompletionParams parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.RequireState(ServerLifecycleState.Initialized, Methods.TextDocumentCompletionName);
        if (!this.documents.TryGetDocument(parameters.TextDocument.Uri, out VectorAnalysisResult? analysis)
            || analysis is null)
        {
            return Task.FromResult(Array.Empty<CompletionItem>());
        }

        int offset = analysis.Document.LineMap.GetOffset(
            new Analysis.Text.TextPosition(parameters.Position.Line, parameters.Position.Character));
        CompletionItem[] items = this.completionService.GetCompletions(analysis, offset)
            .Select(item => new CompletionItem
            {
                Label = item.Label,
                Kind = item.Kind switch
                {
                    VectorCatalogItemKind.Keyword => CompletionItemKind.Keyword,
                    VectorCatalogItemKind.Module => CompletionItemKind.Module,
                    VectorCatalogItemKind.Constant => CompletionItemKind.Constant,
                    VectorCatalogItemKind.Variable => CompletionItemKind.Variable,
                    VectorCatalogItemKind.Parameter => CompletionItemKind.Variable,
                    _ => CompletionItemKind.Function,
                },
                Detail = item.Detail,
                Documentation = item.Documentation,
                InsertText = item.Label,
            }).ToArray();
        return Task.FromResult(items);
    }

    [JsonRpcMethod(Methods.TextDocumentDocumentSymbolName, UseSingleObjectParameterDeserialization = true)]
    public Task<DocumentSymbol[]> DocumentSymbolsAsync(
        DocumentSymbolParams parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.RequireState(ServerLifecycleState.Initialized, Methods.TextDocumentDocumentSymbolName);
        if (!this.documents.TryGetDocument(parameters.TextDocument.Uri, out VectorAnalysisResult? analysis)
            || analysis is null)
        {
            return Task.FromResult(Array.Empty<DocumentSymbol>());
        }

        DocumentSymbol[] symbols = analysis.SemanticModel.Symbols
            .Where(symbol => !symbol.IsSynthetic
                && symbol.ContainingSymbol is null
                && symbol.Kind is not Vector.Analysis.Symbols.VectorSymbolKind.Parameter
                and not Vector.Analysis.Symbols.VectorSymbolKind.LoopVariable)
            .Select(symbol => ToDocumentSymbol(analysis, symbol))
            .ToArray();
        return Task.FromResult(symbols);
    }

    [JsonRpcMethod(Methods.TextDocumentDefinitionName, UseSingleObjectParameterDeserialization = true)]
    public Task<Location?> DefinitionAsync(TextDocumentPositionParams parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.RequireState(ServerLifecycleState.Initialized, Methods.TextDocumentDefinitionName);
        if (!this.documents.TryGetDocument(parameters.TextDocument.Uri, out VectorAnalysisResult? analysis)
            || analysis is null)
        {
            return Task.FromResult<Location?>(null);
        }

        int offset = analysis.Document.LineMap.GetOffset(
            new Analysis.Text.TextPosition(parameters.Position.Line, parameters.Position.Character));
        VectorDefinitionLocation? definition = this.definitionService.GetDefinition(analysis, offset, cancellationToken);
        if (definition is null)
        {
            return Task.FromResult<Location?>(null);
        }

        return Task.FromResult<Location?>(new Location
        {
            Uri = definition.Uri,
            Range = new Microsoft.VisualStudio.LanguageServer.Protocol.Range
            {
                Start = new Position
                {
                    Line = definition.Range.Start.Line,
                    Character = definition.Range.Start.Character,
                },
                End = new Position
                {
                    Line = definition.Range.End.Line,
                    Character = definition.Range.End.Character,
                },
            },
        });
    }

    [JsonRpcMethod(Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
    public Task<Location[]> ReferencesAsync(ReferenceParams parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.RequireState(ServerLifecycleState.Initialized, Methods.TextDocumentReferencesName);
        if (!this.documents.TryGetDocument(parameters.TextDocument.Uri, out VectorAnalysisResult? analysis)
            || analysis is null)
        {
            return Task.FromResult(Array.Empty<Location>());
        }

        int offset = analysis.Document.LineMap.GetOffset(
            new Analysis.Text.TextPosition(parameters.Position.Line, parameters.Position.Character));
        Location[] locations = this.referenceService.GetReferences(
            analysis,
            offset,
            this.documents.Documents,
            parameters.Context.IncludeDeclaration,
            cancellationToken)
            .Select(location => new Location
            {
                Uri = location.Uri,
                Range = new Microsoft.VisualStudio.LanguageServer.Protocol.Range
                {
                    Start = new Position
                    {
                        Line = location.Range.Start.Line,
                        Character = location.Range.Start.Character,
                    },
                    End = new Position
                    {
                        Line = location.Range.End.Line,
                        Character = location.Range.End.Character,
                    },
                },
            })
            .ToArray();
        return Task.FromResult(locations);
    }

    [JsonRpcMethod(Methods.TextDocumentSignatureHelpName, UseSingleObjectParameterDeserialization = true)]
    public Task<SignatureHelp?> SignatureHelpAsync(
        TextDocumentPositionParams parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.RequireState(ServerLifecycleState.Initialized, Methods.TextDocumentSignatureHelpName);
        if (!this.documents.TryGetDocument(parameters.TextDocument.Uri, out VectorAnalysisResult? analysis)
            || analysis is null)
        {
            return Task.FromResult<SignatureHelp?>(null);
        }

        int offset = analysis.Document.LineMap.GetOffset(
            new Analysis.Text.TextPosition(parameters.Position.Line, parameters.Position.Character));
        VectorSignatureInfo? info = this.signatureHelpService.GetSignatureHelp(analysis, offset, cancellationToken);
        if (info is null)
        {
            return Task.FromResult<SignatureHelp?>(null);
        }

        return Task.FromResult<SignatureHelp?>(new SignatureHelp
        {
            ActiveSignature = 0,
            ActiveParameter = info.ActiveParameter,
            Signatures =
            [
                new SignatureInformation
                {
                    Label = info.Label,
                    Documentation = info.Documentation,
                    Parameters = info.Parameters
                        .Select(parameter => new ParameterInformation { Label = parameter })
                        .ToArray(),
                },
            ],
        });
    }

    [JsonRpcMethod(Methods.TextDocumentHoverName, UseSingleObjectParameterDeserialization = true)]
    public Task<Hover?> HoverAsync(TextDocumentPositionParams parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.RequireState(ServerLifecycleState.Initialized, Methods.TextDocumentHoverName);
        if (!this.documents.TryGetDocument(parameters.TextDocument.Uri, out VectorAnalysisResult? analysis)
            || analysis is null)
        {
            return Task.FromResult<Hover?>(null);
        }

        int offset = analysis.Document.LineMap.GetOffset(
            new Analysis.Text.TextPosition(parameters.Position.Line, parameters.Position.Character));
        VectorHoverInfo? info = this.hoverService.GetHover(analysis, offset);
        if (info is null)
        {
            return Task.FromResult<Hover?>(null);
        }

        return Task.FromResult<Hover?>(new Hover
        {
            Contents = new MarkupContent { Kind = MarkupKind.Markdown, Value = info.Markdown },
            Range = new Microsoft.VisualStudio.LanguageServer.Protocol.Range
            {
                Start = new Position { Line = info.Range.Start.Line, Character = info.Range.Start.Character },
                End = new Position { Line = info.Range.End.Line, Character = info.Range.End.Character },
            },
        });
    }

    [JsonRpcMethod("exit")]
    public void Exit()
    {
        lock (this.stateLock)
        {
            this.ExitCode = this.state == ServerLifecycleState.Shutdown ? 0 : 1;
            this.state = ServerLifecycleState.Exited;
        }

        this.completion.TrySetResult();
    }

    public void NotifyDisconnected()
    {
        lock (this.stateLock)
        {
            if (this.state == ServerLifecycleState.Exited)
            {
                return;
            }

            this.ExitCode = this.state == ServerLifecycleState.Shutdown ? 0 : 1;
            this.state = ServerLifecycleState.Disconnected;
        }

        this.completion.TrySetResult();
    }

    private void RequireState(ServerLifecycleState expected, string operation)
    {
        lock (this.stateLock)
        {
            if (this.state != expected)
            {
                throw new InvalidOperationException($"Cannot process '{operation}' while the language server is {this.state}.");
            }
        }
    }

    private static DocumentSymbol ToDocumentSymbol(
        VectorAnalysisResult analysis,
        Vector.Analysis.Symbols.VectorSymbol symbol) => new()
    {
        Name = symbol.Name,
        Kind = symbol.Kind == Vector.Analysis.Symbols.VectorSymbolKind.Function
            ? SymbolKind.Function
            : SymbolKind.Variable,
        Detail = symbol.Kind == Vector.Analysis.Symbols.VectorSymbolKind.Function
            ? $"{symbol.Name}({string.Join(", ", symbol.Parameters)})"
            : null,
        Range = ToLspRange(analysis, symbol.FullSpan),
        SelectionRange = ToLspRange(analysis, symbol.DeclarationSpan),
        Children = symbol.Children
            .Where(child => child.Kind is not Vector.Analysis.Symbols.VectorSymbolKind.Parameter
                and not Vector.Analysis.Symbols.VectorSymbolKind.LoopVariable)
            .Select(child => ToDocumentSymbol(analysis, child))
            .ToArray(),
    };

    private static Microsoft.VisualStudio.LanguageServer.Protocol.Range ToLspRange(
        VectorAnalysisResult analysis,
        Vector.Core.Source.SourceSpan span)
    {
        Analysis.Text.TextRange range = analysis.Document.LineMap.GetRange(span);
        return new Microsoft.VisualStudio.LanguageServer.Protocol.Range
        {
            Start = new Position { Line = range.Start.Line, Character = range.Start.Character },
            End = new Position { Line = range.End.Line, Character = range.End.Character },
        };
    }

    private sealed class NullLanguageClient : ILanguageClient
    {
        public static NullLanguageClient Instance { get; } = new();

        public Task PublishDiagnosticsAsync(PublishDiagnosticParams parameters) => Task.CompletedTask;
    }
}
