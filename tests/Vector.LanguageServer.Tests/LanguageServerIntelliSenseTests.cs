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

    [Fact]
    public async Task Completion_OffersUserSymbolsOnlyInsideTheirScope()
    {
        const string source = "if true {\n let result = 1;\n res\n}\nres";
        Uri uri = new("file:///C:/workspace/scoped-completion.vec");
        VectorLanguageServer server = await CreateServerAsync();
        await OpenAsync(server, uri, source);

        CompletionItem[] inside = await CompleteAsync(server, uri, 2, 4);
        CompletionItem[] outside = await CompleteAsync(server, uri, 4, 3);

        CompletionItem result = Assert.Single(inside, item => item.Label == "result");
        Assert.Equal(CompletionItemKind.Variable, result.Kind);
        Assert.DoesNotContain(outside, item => item.Label == "result");
    }

    [Fact]
    public async Task DocumentSymbols_ReturnDeclarationsWithNestedFunctionStructureAndNameRanges()
    {
        const string source = """
            let top = 1;
            function outer(parameter) {
                let local = parameter;
                function inner(value) { return value; }
            }
            """;
        Uri uri = new("file:///C:/workspace/symbols.vec");
        VectorLanguageServer server = await CreateServerAsync();
        await OpenAsync(server, uri, source);

        DocumentSymbol[] symbols = await server.DocumentSymbolsAsync(new DocumentSymbolParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
        }, CancellationToken.None);

        Assert.Equal(["top", "outer"], symbols.Select(symbol => symbol.Name));
        DocumentSymbol outer = symbols[1];
        Assert.Equal(SymbolKind.Function, outer.Kind);
        Assert.Equal("outer(parameter)", outer.Detail);
        Assert.Equal(1, outer.SelectionRange.Start.Line);
        Assert.Equal(9, outer.SelectionRange.Start.Character);
        Assert.NotNull(outer.Children);
        Assert.Equal(["local", "inner"], outer.Children!.Select(symbol => symbol.Name));
        Assert.Equal(SymbolKind.Function, outer.Children[1].Kind);
    }

    [Fact]
    public async Task Completion_MapsLocalModuleMembersWithoutExecutingTheModule()
    {
        string root = Path.Combine(Path.GetTempPath(), "vector-lsp-module-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "lib"));
        try
        {
            string modulePath = Path.Combine(root, "lib", "geometry.vec");
            await File.WriteAllTextAsync(modulePath, "function area(width, height) { return width * height; }");
            string mainPath = Path.Combine(root, "main.vec");
            Uri uri = new(mainPath);
            const string source = "import lib.geometry;\nlib.geometry.";
            VectorLanguageServer server = await CreateServerAsync();
            await OpenAsync(server, uri, source);

            CompletionItem item = Assert.Single(await CompleteAsync(server, uri, 1, "lib.geometry.".Length));

            Assert.Equal("area", item.Label);
            Assert.Equal(CompletionItemKind.Function, item.Kind);
            Assert.Equal("area(width, height)", item.Detail);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Completion_UsesExplicitProgramRootOutsideTheDocumentDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "vector-lsp-explicit-root-" + Guid.NewGuid().ToString("N"));
        string documentDirectory = Path.Combine(Path.GetTempPath(), "vector-lsp-document-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "tools"));
        Directory.CreateDirectory(documentDirectory);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, "tools", "answers.vec"),
                "let ultimate = 42;");
            Uri uri = new(Path.Combine(documentDirectory, "main.vec"));
            const string source = "import tools.answers;\ntools.answers.";
            var server = new VectorLanguageServer(
                options: new VectorLanguageServerOptions(true, root));
            await server.InitializeAsync(JObject.Parse("{}"), CancellationToken.None);
            await server.InitializedAsync(new object(), CancellationToken.None);
            await OpenAsync(server, uri, source);

            CompletionItem item = Assert.Single(await CompleteAsync(server, uri, 1, "tools.answers.".Length));

            Assert.Equal("ultimate", item.Label);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(documentDirectory, recursive: true);
        }
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

    private static Task<CompletionItem[]> CompleteAsync(
        VectorLanguageServer server,
        Uri uri,
        int line,
        int character) => server.CompletionAsync(new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = line, Character = character },
        }, CancellationToken.None);
}
