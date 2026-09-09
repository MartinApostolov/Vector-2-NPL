namespace Vector.LanguageServer.Tests;

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using Xunit;

public sealed class LanguageServerNavigationTests
{
    [Fact]
    public async Task Definition_MapsLocalSymbolUriAndUtf16RangeToLsp()
    {
        const string source = "let café = \"😀\";\nprint(café);";
        Uri uri = new("file:///C:/workspace/unicode-definition.vec");
        VectorLanguageServer server = await CreateServerAsync(uri, source);

        Location location = Assert.IsType<Location>(await server.DefinitionAsync(new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = 1, Character = 8 },
        }, CancellationToken.None));

        Assert.Equal(uri, location.Uri);
        Assert.Equal(0, location.Range.Start.Line);
        Assert.Equal(4, location.Range.Start.Character);
        Assert.Equal(8, location.Range.End.Character);
    }

    [Fact]
    public async Task Definition_ReturnsNullForBuiltinAndUnknownDocument()
    {
        Uri uri = new("file:///C:/workspace/no-definition.vec");
        VectorLanguageServer server = await CreateServerAsync(uri, "print(1);");

        Assert.Null(await server.DefinitionAsync(new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = 0, Character = 2 },
        }, CancellationToken.None));
        Assert.Null(await server.DefinitionAsync(new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri("file:///C:/workspace/missing.vec") },
            Position = new Position(),
        }, CancellationToken.None));
    }

    private static async Task<VectorLanguageServer> CreateServerAsync(Uri uri, string source)
    {
        var server = new VectorLanguageServer();
        await server.InitializeAsync(JObject.Parse("{}"), CancellationToken.None);
        await server.InitializedAsync(new object(), CancellationToken.None);
        await server.DidOpenAsync(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = uri,
                LanguageId = "vector",
                Version = 1,
                Text = source,
            },
        }, CancellationToken.None);
        return server;
    }
}
