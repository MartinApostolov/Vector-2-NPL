namespace Vector.LanguageServer.Tests;

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using Xunit;

public sealed class LanguageServerRenameTests
{
    [Fact]
    public async Task Rename_ReturnsWorkspaceEditsForResolvedLocations()
    {
        const string source = "let value = 1;\nprint(value);";
        Uri uri = new("file:///C:/workspace/rename.vec");
        VectorLanguageServer server = await CreateServerAsync(uri, source);

        WorkspaceEdit edit = Assert.IsType<WorkspaceEdit>(await server.RenameAsync(new RenameParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = 1, Character = 8 },
            NewName = "result",
        }, CancellationToken.None));

        TextEdit[] changes = Assert.Single(edit.Changes!).Value;
        Assert.Equal(2, changes.Length);
        Assert.All(changes, change => Assert.Equal("result", change.NewText));
    }

    [Fact]
    public async Task Rename_RejectsInvalidOrConflictingNamesAndUnknownSymbols()
    {
        const string source = "let first = 1;\nlet second = first;\nmissing;";
        Uri uri = new("file:///C:/workspace/invalid-rename.vec");
        VectorLanguageServer server = await CreateServerAsync(uri, source);

        await Assert.ThrowsAsync<InvalidOperationException>(() => server.RenameAsync(new RenameParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = 1, Character = 15 },
            NewName = "second",
        }, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => server.RenameAsync(new RenameParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = 1, Character = 15 },
            NewName = "not valid",
        }, CancellationToken.None));
        Assert.Null(await server.RenameAsync(new RenameParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = 2, Character = 2 },
            NewName = "known",
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
