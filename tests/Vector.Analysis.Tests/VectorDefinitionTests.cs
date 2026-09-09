namespace Vector.Analysis.Tests;

using Vector.Analysis.Documents;
using Vector.Analysis.Modules;
using Vector.Analysis.Navigation;
using Xunit;

public sealed class VectorDefinitionTests
{
    [Fact]
    public void LocalReference_NavigatesToTheSameDocumentDeclaration()
    {
        const string source = "let answer = 42;\nprint(answer);";
        VectorAnalysisResult analysis = Analyze(source);
        var service = new VectorDefinitionService(new VectorModuleIndex());

        VectorDefinitionLocation definition = Assert.IsType<VectorDefinitionLocation>(
            service.GetDefinition(analysis, source.LastIndexOf("answer", StringComparison.Ordinal) + 2));

        Assert.Equal(analysis.Document.Uri, definition.Uri);
        Assert.Equal(0, definition.Range.Start.Line);
        Assert.Equal(4, definition.Range.Start.Character);
        Assert.Equal(10, definition.Range.End.Character);
    }

    [Fact]
    public void DeclarationName_NavigatesToItself()
    {
        const string source = "function calculate(value) { return value; }";
        VectorAnalysisResult analysis = Analyze(source);
        var service = new VectorDefinitionService(new VectorModuleIndex());

        VectorDefinitionLocation definition = Assert.IsType<VectorDefinitionLocation>(
            service.GetDefinition(analysis, source.IndexOf("calculate", StringComparison.Ordinal) + 1));

        Assert.Equal(9, definition.Range.Start.Character);
        Assert.Equal(18, definition.Range.End.Character);
    }

    [Fact]
    public void ShadowedReferences_NavigateToTheirEffectiveDeclarations()
    {
        const string source = """
            let value = 0;
            if true {
                let value = 1;
                print(value);
            }
            print(value);
            """;
        VectorAnalysisResult analysis = Analyze(source);
        var service = new VectorDefinitionService(new VectorModuleIndex());

        VectorDefinitionLocation inner = Assert.IsType<VectorDefinitionLocation>(service.GetDefinition(
            analysis,
            source.IndexOf("print(value)", StringComparison.Ordinal) + "print(".Length + 1));
        VectorDefinitionLocation outer = Assert.IsType<VectorDefinitionLocation>(service.GetDefinition(
            analysis,
            source.LastIndexOf("print(value)", StringComparison.Ordinal) + "print(".Length + 1));

        Assert.Equal(2, inner.Range.Start.Line);
        Assert.Equal(0, outer.Range.Start.Line);
    }

    [Fact]
    public void ParametersNestedFunctionsAndAssignmentTargets_AreBound()
    {
        const string source = """
            function outer(parameter) {
                function nested() { return parameter; }
                parameter = nested();
            }
            """;
        VectorAnalysisResult analysis = Analyze(source);
        var service = new VectorDefinitionService(new VectorModuleIndex());

        VectorDefinitionLocation parameterUse = Assert.IsType<VectorDefinitionLocation>(service.GetDefinition(
            analysis,
            source.IndexOf("return parameter", StringComparison.Ordinal) + "return ".Length + 1));
        VectorDefinitionLocation assignmentTarget = Assert.IsType<VectorDefinitionLocation>(service.GetDefinition(
            analysis,
            source.LastIndexOf("parameter", StringComparison.Ordinal) + 1));
        VectorDefinitionLocation nestedCall = Assert.IsType<VectorDefinitionLocation>(service.GetDefinition(
            analysis,
            source.LastIndexOf("nested", StringComparison.Ordinal) + 1));

        Assert.Equal(15, parameterUse.Range.Start.Character);
        Assert.Equal(parameterUse, assignmentTarget);
        Assert.Equal(13, nestedCall.Range.Start.Character);
    }

    [Fact]
    public void UndefinedAndNativeSymbols_DoNotProduceFakeLocations()
    {
        const string source = "import lib.math;\nprint(missing);\nlib.math.sqrt(9);";
        VectorAnalysisResult analysis = Analyze(source);
        var service = new VectorDefinitionService(new VectorModuleIndex());

        Assert.Null(service.GetDefinition(analysis, source.IndexOf("print", StringComparison.Ordinal) + 1));
        Assert.Null(service.GetDefinition(analysis, source.IndexOf("missing", StringComparison.Ordinal) + 1));
        Assert.Null(service.GetDefinition(analysis, source.IndexOf("sqrt", StringComparison.Ordinal) + 1));
    }

    [Fact]
    public void UnicodeIdentifiers_UseUtf16DefinitionPositions()
    {
        const string source = "let café = \"😀\";\nprint(café);";
        VectorAnalysisResult analysis = Analyze(source);

        VectorDefinitionLocation definition = Assert.IsType<VectorDefinitionLocation>(
            new VectorDefinitionService(new VectorModuleIndex()).GetDefinition(
                analysis,
                source.LastIndexOf("café", StringComparison.Ordinal) + 2));

        Assert.Equal(4, definition.Range.Start.Character);
        Assert.Equal(8, definition.Range.End.Character);
    }

    [Fact]
    public void QualifiedMemberAndImport_NavigateAcrossFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "vector-definitions-" + Guid.NewGuid().ToString("N"));
        string moduleDirectory = Path.Combine(root, "lib");
        Directory.CreateDirectory(moduleDirectory);
        try
        {
            string modulePath = Path.Combine(moduleDirectory, "geometry.vec");
            File.WriteAllText(modulePath, "let origin = [0, 0];\nfunction area(width, height) { return width * height; }");
            const string source = "import lib.geometry;\nlib.geometry.area(2, 3);";
            VectorAnalysisResult analysis = new VectorAnalyzer().Analyze(VectorDocumentSnapshot.FromFile(
                Path.Combine(root, "main.vec"),
                source,
                0));
            var service = new VectorDefinitionService(new VectorModuleIndex());

            VectorDefinitionLocation member = Assert.IsType<VectorDefinitionLocation>(service.GetDefinition(
                analysis,
                source.LastIndexOf("area", StringComparison.Ordinal) + 1));
            VectorDefinitionLocation importedModule = Assert.IsType<VectorDefinitionLocation>(service.GetDefinition(
                analysis,
                source.IndexOf("geometry", StringComparison.Ordinal) + 1));

            Assert.Equal(Path.GetFullPath(modulePath), member.Uri.LocalPath);
            Assert.Equal(1, member.Range.Start.Line);
            Assert.Equal(9, member.Range.Start.Character);
            Assert.Equal(Path.GetFullPath(modulePath), importedModule.Uri.LocalPath);
            Assert.Equal(0, importedModule.Range.Start.Line);
            Assert.Equal(0, importedModule.Range.Start.Character);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static VectorAnalysisResult Analyze(string source) =>
        new VectorAnalyzer().Analyze(VectorDocumentSnapshot.CreateInMemory(source, "definitions.vec"));
}
