namespace Vector.Analysis.Tests;

using Vector.Analysis.Documents;
using Vector.Analysis.IntelliSense;
using Vector.Analysis.Symbols;
using Xunit;

public sealed class VectorScopeAnalysisTests
{
    [Fact]
    public void NestedBlock_ExposesLocalInsideButNotOutsideItsLexicalScope()
    {
        const string source = """
            if true {
                let result = 42;
                print(result);
            }
            print("outside");
            """;
        VectorAnalysisResult analysis = Analyze(source);

        IReadOnlyList<VectorSymbol> inside = VisibleAt(analysis, source.IndexOf("print(result)", StringComparison.Ordinal));
        IReadOnlyList<VectorSymbol> outside = VisibleAt(analysis, source.IndexOf("print(\"outside\")", StringComparison.Ordinal));

        Assert.Contains(inside, symbol => symbol.Name == "result");
        Assert.DoesNotContain(outside, symbol => symbol.Name == "result");
    }

    [Fact]
    public void InnerDeclaration_ShadowsOuterOnlyAfterTheDeclarationRuns()
    {
        const string source = """
            let value = 1;
            if true {
                print(value);
                let value = 2;
                print(value);
            }
            """;
        VectorAnalysisResult analysis = Analyze(source);
        VectorSymbol[] declarations = analysis.SemanticModel.Symbols
            .Where(symbol => symbol.Name == "value")
            .ToArray();
        VectorSymbol outer = declarations[0];
        VectorSymbol inner = declarations[1];

        VectorSymbol before = VisibleAt(analysis, source.IndexOf("print(value)", StringComparison.Ordinal))
            .Single(symbol => symbol.Name == "value");
        VectorSymbol after = VisibleAt(analysis, source.LastIndexOf("print(value)", StringComparison.Ordinal))
            .Single(symbol => symbol.Name == "value");

        Assert.Same(outer, before);
        Assert.Same(inner, after);
    }

    [Fact]
    public void FunctionScopes_ContainParametersLocalsAndNestedFunctions()
    {
        const string source = """
            function calculate(input, scale) {
                let result = input;
                function adjust(delta) {
                    print(delta);
                }
                print(result);
            }
            """;
        VectorAnalysisResult analysis = Analyze(source);

        IReadOnlyList<VectorSymbol> outerBody = VisibleAt(
            analysis,
            source.LastIndexOf("print(result)", StringComparison.Ordinal));
        IReadOnlyList<VectorSymbol> nestedBody = VisibleAt(
            analysis,
            source.IndexOf("print(delta)", StringComparison.Ordinal));

        Assert.Contains(outerBody, symbol => symbol.Name == "input" && symbol.Kind == VectorSymbolKind.Parameter);
        Assert.Contains(outerBody, symbol => symbol.Name == "scale" && symbol.Kind == VectorSymbolKind.Parameter);
        Assert.Contains(outerBody, symbol => symbol.Name == "result" && symbol.Kind == VectorSymbolKind.Variable);
        Assert.Contains(outerBody, symbol => symbol.Name == "adjust" && symbol.Kind == VectorSymbolKind.Function);
        Assert.Contains(nestedBody, symbol => symbol.Name == "delta" && symbol.Kind == VectorSymbolKind.Parameter);
        Assert.Contains(nestedBody, symbol => symbol.Name == "calculate" && symbol.Kind == VectorSymbolKind.Function);
    }

    [Fact]
    public void ForLoopVariable_SharesTheBodyScopeAndDoesNotLeak()
    {
        const string source = """
            for item in range(0, 2) {
                let doubled = item + item;
                print(doubled);
            }
            print("done");
            """;
        VectorAnalysisResult analysis = Analyze(source);

        IReadOnlyList<VectorSymbol> inside = VisibleAt(analysis, source.IndexOf("print(doubled)", StringComparison.Ordinal));
        IReadOnlyList<VectorSymbol> outside = VisibleAt(analysis, source.IndexOf("print(\"done\")", StringComparison.Ordinal));

        Assert.Contains(inside, symbol => symbol.Name == "item" && symbol.Kind == VectorSymbolKind.LoopVariable);
        Assert.Contains(inside, symbol => symbol.Name == "doubled");
        Assert.DoesNotContain(outside, symbol => symbol.Name is "item" or "doubled");
    }

    [Fact]
    public void DuplicateDeclarations_AreRetainedForStructureButFirstBindingRemainsVisible()
    {
        const string source = "let value = 1;\nlet value = 2;\nprint(value);";
        VectorAnalysisResult analysis = Analyze(source);
        VectorSymbol[] declarations = analysis.SemanticModel.Symbols.Where(symbol => symbol.Name == "value").ToArray();

        Assert.Equal(2, declarations.Length);
        Assert.False(declarations[0].IsDuplicate);
        Assert.True(declarations[1].IsDuplicate);
        Assert.Same(
            declarations[0],
            VisibleAt(analysis, source.IndexOf("print", StringComparison.Ordinal)).Single(symbol => symbol.Name == "value"));
    }

    [Fact]
    public void IncompleteFunctionBody_StillProvidesParametersAndCompletedLocalsAtEof()
    {
        const string source = "function draft(parameter) {\n    let local = 1;\n    ";
        VectorAnalysisResult analysis = Analyze(source);

        IReadOnlyList<VectorSymbol> visible = analysis.SemanticModel.GetVisibleSymbols(source.Length);

        Assert.True(analysis.HasErrors);
        Assert.Contains(visible, symbol => symbol.Name == "parameter" && symbol.Kind == VectorSymbolKind.Parameter);
        Assert.Contains(visible, symbol => symbol.Name == "local" && symbol.Kind == VectorSymbolKind.Variable);
        Assert.Contains(visible, symbol => symbol.Name == "draft" && symbol.Kind == VectorSymbolKind.Function);
    }

    [Fact]
    public void SymbolCompletion_UsesVisibleSymbolsWithoutClaimingStaticTypes()
    {
        const string source = "let result = 1;\nres";
        VectorAnalysisResult analysis = Analyze(source);

        IReadOnlyList<VectorCatalogItem> items = new VectorCompletionService()
            .GetCompletions(analysis, source.Length);

        VectorCatalogItem result = Assert.Single(items, item => item.Label == "result");
        Assert.Equal(VectorCatalogItemKind.Variable, result.Kind);
        Assert.Equal("Vector variable", result.Detail);
        Assert.DoesNotContain("number", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingDeclarationName_IsRetainedForRecoveryButNeverMadeVisible()
    {
        const string source = "let = 1;\n";
        VectorAnalysisResult analysis = Analyze(source);

        Assert.Contains(analysis.SemanticModel.Symbols, symbol => symbol.IsSynthetic);
        Assert.DoesNotContain(
            analysis.SemanticModel.GetVisibleSymbols(source.Length),
            symbol => symbol.IsSynthetic);
    }

    private static IReadOnlyList<VectorSymbol> VisibleAt(VectorAnalysisResult analysis, int offset) =>
        analysis.SemanticModel.GetVisibleSymbols(offset);

    private static VectorAnalysisResult Analyze(string source) =>
        new VectorAnalyzer().Analyze(VectorDocumentSnapshot.CreateInMemory(source, "scopes.vec"));
}
