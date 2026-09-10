namespace Vector.LanguageServer.Tests;

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using Xunit;

public sealed class LanguageServerDiagnosticsTests
{
    [Fact]
    public async Task DidOpen_PublishesParserDiagnosticsForInMemoryText()
    {
        var client = new RecordingLanguageClient();
        VectorLanguageServer server = await CreateServerAsync(client);
        Uri uri = new("file:///C:/workspace/error.vec");

        await server.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = uri,
                LanguageId = "vector",
                Version = 1,
                Text = "let value = ;",
            },
        }, CancellationToken.None);

        PublishDiagnosticParams published = Assert.Single(client.Publications);
        Assert.Equal(uri, published.Uri);
        var diagnostic = Assert.Single(published.Diagnostics);
        Assert.Equal("Vector", diagnostic.Source);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Code?.ToString()));
    }

    [Fact]
    public async Task EncodedWindowsDriveUri_RemainsTheDocumentIdentityForDiagnosticsAndCompletion()
    {
        var client = new RecordingLanguageClient();
        VectorLanguageServer server = await CreateServerAsync(client);
        Uri uri = new("file:///C%3A/workspace/encoded-drive.vec");

        await OpenAsync(server, uri, "pri", 1);

        Assert.Equal(uri, Assert.Single(client.Publications).Uri);
        CompletionItem[] completion = await server.CompletionAsync(new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = 0, Character = 3 },
        }, CancellationToken.None);
        Assert.Contains(completion, item => item.Label == "print");
    }

    [Fact]
    public async Task DidChange_ClearsFixedDiagnostics()
    {
        var client = new RecordingLanguageClient();
        VectorLanguageServer server = await CreateServerAsync(client);
        Uri uri = new("file:///C:/workspace/fixed.vec");
        await OpenAsync(server, uri, "let value = ;", 1);
        client.Publications.Clear();

        await ChangeAsync(server, uri, "let value = 1;", 2);

        PublishDiagnosticParams published = Assert.Single(client.Publications);
        Assert.Empty(published.Diagnostics);
    }

    [Fact]
    public async Task RapidChanges_DoNotPublishCancelledStaleAnalysis()
    {
        var client = new RecordingLanguageClient();
        VectorLanguageServer server = await CreateServerAsync(client);
        Uri uri = new("file:///C:/workspace/typing.vec");
        await OpenAsync(server, uri, "let value = 0;", 1);
        client.Publications.Clear();

        Task stale = ChangeAsync(server, uri, "let value = ;", 2);
        Task current = ChangeAsync(server, uri, "let value = 2;", 3);
        await Task.WhenAll(stale, current);

        PublishDiagnosticParams published = Assert.Single(client.Publications);
        Assert.Empty(published.Diagnostics);
    }

    [Fact]
    public async Task OlderVersion_CannotOverwriteCurrentDiagnostics()
    {
        var client = new RecordingLanguageClient();
        VectorLanguageServer server = await CreateServerAsync(client);
        Uri uri = new("file:///C:/workspace/version.vec");
        await OpenAsync(server, uri, "let value = 0;", 1);
        client.Publications.Clear();

        await ChangeAsync(server, uri, "let value = 3;", 3);
        await ChangeAsync(server, uri, "let value = ;", 2);

        PublishDiagnosticParams published = Assert.Single(client.Publications);
        Assert.Empty(published.Diagnostics);
    }

    [Fact]
    public async Task UnicodeDiagnostic_UsesUtf16Range()
    {
        var client = new RecordingLanguageClient();
        VectorLanguageServer server = await CreateServerAsync(client);
        Uri uri = new("file:///C:/workspace/unicode.vec");

        await OpenAsync(server, uri, "let value = 1;\r\n😀", 1);

        var diagnostic = Assert.Single(Assert.Single(client.Publications).Diagnostics);
        Assert.Equal(1, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
        Assert.Equal(2, diagnostic.Range.End.Character);
    }

    [Fact]
    public async Task DidClose_RemovesAndClearsDiagnostics()
    {
        var client = new RecordingLanguageClient();
        VectorLanguageServer server = await CreateServerAsync(client);
        Uri uri = new("file:///C:/workspace/closed.vec");
        await OpenAsync(server, uri, "let value = ;", 1);
        client.Publications.Clear();

        await server.DidCloseAsync(new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
        }, CancellationToken.None);

        Assert.Empty(Assert.Single(client.Publications).Diagnostics);
    }

    [Fact]
    public async Task LiveDiagnosticsOff_PublishesAnEmptySetForMalformedSource()
    {
        var client = new RecordingLanguageClient();
        var server = new VectorLanguageServer(client, new VectorLanguageServerOptions(false, null));
        await server.InitializeAsync(JObject.Parse("{}"), CancellationToken.None);
        await server.InitializedAsync(new object(), CancellationToken.None);

        await OpenAsync(server, new Uri("file:///C:/workspace/disabled.vec"), "let value = ;", 1);

        Assert.Empty(Assert.Single(client.Publications).Diagnostics);
    }

    private static async Task<VectorLanguageServer> CreateServerAsync(ILanguageClient client)
    {
        var server = new VectorLanguageServer(client);
        await server.InitializeAsync(JObject.Parse("{}"), CancellationToken.None);
        await server.InitializedAsync(new object(), CancellationToken.None);
        return server;
    }

    private static Task OpenAsync(VectorLanguageServer server, Uri uri, string text, int version) =>
        server.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = uri,
                LanguageId = "vector",
                Version = version,
                Text = text,
            },
        }, CancellationToken.None);

    private static Task ChangeAsync(VectorLanguageServer server, Uri uri, string text, int version) =>
        server.DidChangeAsync(new DidChangeTextDocumentParams
        {
            TextDocument = new VersionedTextDocumentIdentifier { Uri = uri, Version = version },
            ContentChanges = [new TextDocumentContentChangeEvent { Text = text }],
        }, CancellationToken.None);

    private sealed class RecordingLanguageClient : ILanguageClient
    {
        public List<PublishDiagnosticParams> Publications { get; } = [];

        public Task PublishDiagnosticsAsync(PublishDiagnosticParams parameters)
        {
            this.Publications.Add(parameters);
            return Task.CompletedTask;
        }
    }
}
