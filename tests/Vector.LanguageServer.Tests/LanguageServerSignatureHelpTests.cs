namespace Vector.LanguageServer.Tests;

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using Xunit;

public sealed class LanguageServerSignatureHelpTests
{
    [Fact]
    public async Task SignatureHelp_MapsNestedIncompleteCallToLsp()
    {
        const string source = "range(1, concat(\"left\", ";
        Uri uri = new("file:///C:/workspace/signature.vec");
        VectorLanguageServer server = await CreateServerAsync(uri, source);

        SignatureHelp signatureHelp = Assert.IsType<SignatureHelp>(await server.SignatureHelpAsync(
            new TextDocumentPositionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri },
                Position = new Position { Line = 0, Character = source.Length },
            },
            CancellationToken.None));

        SignatureInformation signature = Assert.Single(signatureHelp.Signatures);
        Assert.Equal("concat(left, right)", signature.Label);
        Assert.Equal(1, signatureHelp.ActiveParameter);
        Assert.NotNull(signature.Parameters);
        Assert.Equal(2, signature.Parameters!.Length);
    }

    [Fact]
    public async Task SignatureHelp_ReturnsNullForUnknownDocument()
    {
        var server = new VectorLanguageServer();
        await server.InitializeAsync(JObject.Parse("{}"), CancellationToken.None);
        await server.InitializedAsync(new object(), CancellationToken.None);

        Assert.Null(await server.SignatureHelpAsync(new TextDocumentPositionParams
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
