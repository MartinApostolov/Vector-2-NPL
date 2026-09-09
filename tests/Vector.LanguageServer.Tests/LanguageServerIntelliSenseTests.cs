namespace Vector.LanguageServer.Tests;

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

public sealed class LanguageServerIntelliSenseTests
{
    [Fact]
    public async Task Completion_MapsImportedStandardModuleMembersToLspItems()
    {
        const string source = "import lib.vector;\nlib.vector.";
        Uri uri = new("file:///C:/workspace/completion.vec");
        VectorLanguageServer server = await CreateServerAsync();
        await OpenAsync(server, uri, source);

        CompletionItem[] items = await server.CompletionAsync(new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = 1, Character = "lib.vector.".Length },
        }, CancellationToken.None);

        Assert.Equal(["dot", "magnitude", "normalize"], items.Select(item => item.Label));
        Assert.All(items, item => Assert.Equal(CompletionItemKind.Function, item.Kind));
        Assert.Contains(items, item => item.Label == "dot" && item.Detail == "dot(a, b)");
    }

    [Fact]
    public async Task Hover_MapsMarkdownAndUtf16RangeToLsp()
    {
        const string source = "😀 range(1, 4);";
        Uri uri = new("file:///C:/workspace/hover.vec");
        VectorLanguageServer server = await CreateServerAsync();
        await OpenAsync(server, uri, source);

        Hover hover = Assert.IsType<Hover>(await server.HoverAsync(new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = 0, Character = 5 },
        }, CancellationToken.None));

        JObject serialized = JObject.Parse(JsonConvert.SerializeObject(hover));
        Assert.Equal("markdown", serialized["contents"]?["kind"]?.Value<string>());
        Assert.Contains("range(start, end)", serialized["contents"]?["value"]?.Value<string>(), StringComparison.Ordinal);
        Assert.NotNull(hover.Range);
        Assert.Equal(3, hover.Range!.Start.Character);
        Assert.Equal(8, hover.Range.End.Character);
    }

    [Fact]
    public async Task CompletionAndHover_ReturnEmptyForAnUnknownDocument()
    {
        VectorLanguageServer server = await CreateServerAsync();
        var textDocument = new TextDocumentIdentifier { Uri = new Uri("file:///C:/workspace/missing.vec") };

        CompletionItem[] completion = await server.CompletionAsync(new CompletionParams
        {
            TextDocument = textDocument,
            Position = new Position(),
        }, CancellationToken.None);
        Hover? hover = await server.HoverAsync(new TextDocumentPositionParams
        {
            TextDocument = textDocument,
            Position = new Position(),
        }, CancellationToken.None);

        Assert.Empty(completion);
        Assert.Null(hover);
    }

    private static async Task<VectorLanguageServer> CreateServerAsync()
    {
        var server = new VectorLanguageServer();
        await server.InitializeAsync(JObject.Parse("{}"), CancellationToken.None);
        await server.InitializedAsync(new object(), CancellationToken.None);
        return server;
    }

    private static Task OpenAsync(VectorLanguageServer server, Uri uri, string source) =>
        server.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = uri,
                LanguageId = "vector",
                Version = 1,
                Text = source,
            },
        }, CancellationToken.None);
}
