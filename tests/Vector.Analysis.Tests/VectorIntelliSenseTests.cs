namespace Vector.Analysis.Tests;

using Vector.Analysis.Documents;
using Vector.Analysis.IntelliSense;
using Xunit;

public sealed class VectorIntelliSenseTests
{
    private readonly VectorCompletionService completion = new();
    private readonly VectorHoverService hover = new();

    [Fact]
    public void GlobalCompletion_ContainsKeywordsBuiltinsAndStandardModules()
    {
        VectorAnalysisResult analysis = Analyze(string.Empty);

        IReadOnlyList<VectorCatalogItem> items = this.completion.GetCompletions(analysis, 0);

        Assert.Contains(items, item => item.Label == "function" && item.Kind == VectorCatalogItemKind.Keyword);
        Assert.Contains(items, item => item.Label == "range" && item.Kind == VectorCatalogItemKind.Function);
        Assert.Contains(items, item => item.Label == "lib.matrix" && item.Kind == VectorCatalogItemKind.Module);
    }

    [Fact]
    public void GlobalCompletion_FiltersUsingTheIdentifierPrefix()
    {
        const string source = "con";
        VectorAnalysisResult analysis = Analyze(source);

        IReadOnlyList<VectorCatalogItem> items = this.completion.GetCompletions(analysis, source.Length);

        Assert.Equal(["concat", "continue"], items.Select(item => item.Label));
    }

    [Fact]
    public void QualifiedCompletion_OffersMembersForAnImportedStandardModule()
    {
        const string source = "import lib.vector;\nlib.vector.";
        VectorAnalysisResult analysis = Analyze(source);

        IReadOnlyList<VectorCatalogItem> items = this.completion.GetCompletions(analysis, source.Length);

        Assert.Equal(["dot", "magnitude", "normalize"], items.Select(item => item.Label));
        Assert.All(items, item => Assert.Equal(VectorCatalogItemKind.Function, item.Kind));
    }

    [Fact]
    public void QualifiedCompletion_RequiresTheStandardModuleImport()
    {
        const string source = "lib.vector.";
        VectorAnalysisResult analysis = Analyze(source);

        Assert.Empty(this.completion.GetCompletions(analysis, source.Length));
    }

    [Fact]
    public void GlobalCompletion_DoesNotLeakStandardModuleMembers()
    {
        VectorAnalysisResult analysis = Analyze("import lib.vector;\n");

        IReadOnlyList<VectorCatalogItem> items = this.completion.GetCompletions(
            analysis,
            analysis.Document.Text.Length);

        Assert.DoesNotContain(items, item => item.Label is "dot" or "magnitude" or "normalize");
    }

    [Fact]
    public void PartialModuleCompletion_OffersQualifiedModuleNames()
    {
        const string source = "lib.";
        VectorAnalysisResult analysis = Analyze(source);

        IReadOnlyList<VectorCatalogItem> items = this.completion.GetCompletions(analysis, source.Length);

        Assert.Equal(5, items.Count);
        Assert.All(items, item => Assert.StartsWith("lib.", item.Label, StringComparison.Ordinal));
    }

    [Fact]
    public void Hover_ProvidesBuiltinSignatureDocumentationAndUtf16Range()
    {
        const string source = "😀 range(1, 4);";
        VectorAnalysisResult analysis = Analyze(source);

        VectorHoverInfo info = Assert.IsType<VectorHoverInfo>(
            this.hover.GetHover(analysis, source.IndexOf("range", StringComparison.Ordinal) + 2));

        Assert.Contains("range(start, end)", info.Markdown, StringComparison.Ordinal);
        Assert.Contains("inclusive", info.Markdown, StringComparison.Ordinal);
        Assert.Equal(3, info.Range.Start.Character);
        Assert.Equal(8, info.Range.End.Character);
    }

    [Fact]
    public void Hover_ProvidesQualifiedStandardLibrarySignature()
    {
        const string source = "import lib.vector;\nlib.vector.dot([1], [2]);";
        VectorAnalysisResult analysis = Analyze(source);
        int offset = source.IndexOf("dot", StringComparison.Ordinal) + 1;

        VectorHoverInfo info = Assert.IsType<VectorHoverInfo>(this.hover.GetHover(analysis, offset));

        Assert.Contains("lib.vector.dot(a, b)", info.Markdown, StringComparison.Ordinal);
        Assert.Equal(1, info.Range.Start.Line);
        Assert.Equal(0, info.Range.Start.Character);
    }

    [Fact]
    public void Hover_ProvidesLocalVariableAndFunctionDescriptions()
    {
        const string source = "let area = 42;\nfunction rectangle(width, height) { return width * height; }\nprint(area);\nrectangle(6, 7);";
        VectorAnalysisResult analysis = Analyze(source);

        VectorHoverInfo variable = Assert.IsType<VectorHoverInfo>(
            this.hover.GetHover(analysis, source.LastIndexOf("area", StringComparison.Ordinal) + 1));
        VectorHoverInfo function = Assert.IsType<VectorHoverInfo>(
            this.hover.GetHover(analysis, source.LastIndexOf("rectangle", StringComparison.Ordinal) + 1));

        Assert.Contains("area", variable.Markdown, StringComparison.Ordinal);
        Assert.Contains("Vector variable", variable.Markdown, StringComparison.Ordinal);
        Assert.Contains("rectangle(width, height)", function.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownName_HasNoHover()
    {
        VectorAnalysisResult analysis = Analyze("mystery;");

        Assert.Null(this.hover.GetHover(analysis, 2));
    }

    [Fact]
    public void ToolingCatalog_IsExplicitAndDoesNotReferencePluginInfrastructure()
    {
        Assert.Equal(17, VectorLanguageCatalog.Keywords.Count);
        Assert.Equal(
            ["concat", "length", "number", "print", "range", "text", "type"],
            VectorLanguageCatalog.Builtins.Select(item => item.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["lib.collections", "lib.io", "lib.math", "lib.matrix", "lib.vector"],
            VectorLanguageCatalog.StandardModules.Select(item => item.QualifiedName).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(
            typeof(VectorLanguageCatalog).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "Vector.Plugins");
    }

    private static VectorAnalysisResult Analyze(string source) =>
        new VectorAnalyzer().Analyze(VectorDocumentSnapshot.CreateInMemory(source, "intellisense.vec"));
}
