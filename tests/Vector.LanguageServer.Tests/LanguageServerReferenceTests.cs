namespace Vector.LanguageServer.Tests;

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using Xunit;

public sealed class LanguageServerReferenceTests
{
    [Fact]
    public async Task References_MapsResolvedSymbolLocationsAndDeclarationPreference()
    {
        const string source = "let value = 1;\nprint(value);\nvalue = 2;";
        Uri uri = new("file:///C:/workspace/references.vec");
        VectorLanguageServer server = await CreateServerAsync(uri, source);

        Location[] withDeclaration = await FindAsync(server, uri, 1, 8, includeDeclaration: true);
        Location[] withoutDeclaration = await FindAsync(server, uri, 1, 8, includeDeclaration: false);

        Assert.Equal(3, withDeclaration.Length);
        Assert.Equal(2, withoutDeclaration.Length);
        Assert.All(withDeclaration, location => Assert.Equal(uri, location.Uri));
        Assert.Contains(withDeclaration, location => location.Range.Start.Line == 0 && location.Range.Start.Character == 4);
    }

    [Fact]
    public async Task References_ReturnsEmptyForUnknownDocumentAndUndefinedName()
    {
        Uri uri = new("file:///C:/workspace/no-references.vec");
        VectorLanguageServer server = await CreateServerAsync(uri, "missing;");

        Assert.Empty(await FindAsync(server, uri, 0, 2, includeDeclaration: true));
        Assert.Empty(await FindAsync(
            server,
            new Uri("file:///C:/workspace/unknown.vec"),
            0,
            0,
            includeDeclaration: true));
    }

    [Fact]
    public async Task References_TracksLocalModuleMemberAcrossOpenDocuments()
    {
        string root = Path.Combine(Path.GetTempPath(), "vector-lsp-references-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "local"));
        try
        {
            string modulePath = Path.Combine(root, "local", "values.vec");
            string mainPath = Path.Combine(root, "main.vec");
            const string moduleSource = "let answer = 42;\nprint(answer);";
            const string mainSource = "import local.values;\nlocal.values.answer;";
            await File.WriteAllTextAsync(modulePath, moduleSource);
            var server = new VectorLanguageServer(options: new VectorLanguageServerOptions(true, root));
            await server.InitializeAsync(JObject.Parse("{}"), CancellationToken.None);
            await server.InitializedAsync(new object(), CancellationToken.None);
            await OpenAsync(server, new Uri(modulePath), moduleSource);
            await OpenAsync(server, new Uri(mainPath), mainSource);

            Location[] locations = await FindAsync(
                server,
                new Uri(modulePath),
                0,
                6,
                includeDeclaration: true);

            Assert.Equal(3, locations.Length);
            Assert.Contains(locations, location => location.Uri == new Uri(mainPath));
            Assert.Equal(2, locations.Count(location => location.Uri == new Uri(modulePath)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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

    private static Task<Location[]> FindAsync(
        VectorLanguageServer server,
        Uri uri,
        int line,
        int character,
        bool includeDeclaration) => server.ReferencesAsync(new ReferenceParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = line, Character = character },
            Context = new ReferenceContext { IncludeDeclaration = includeDeclaration },
        }, CancellationToken.None);
}
