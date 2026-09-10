namespace Vector.Analysis.Tests;

using Vector.Analysis.Documents;
using Vector.Analysis.Modules;
using Vector.Analysis.Navigation;
using Xunit;

public sealed class VectorReferenceTests
{
    [Fact]
    public void LocalVariable_ReportsDeclarationReadsAndAssignmentTargets()
    {
        const string source = "let value = 1;\nprint(value);\nvalue = value + 1;";
        VectorAnalysisResult analysis = Analyze(source);
        var service = new VectorReferenceService(new VectorModuleIndex());

        IReadOnlyList<VectorReferenceLocation> references = service.GetReferences(
            analysis,
            source.IndexOf("value", StringComparison.Ordinal) + 1,
            [analysis],
            includeDeclaration: true);

        Assert.Equal(4, references.Count);
        Assert.Single(references, location => location.IsDeclaration);
        Assert.Equal([4, 6, 0, 8], references.Select(location => location.Range.Start.Character));
    }

    [Fact]
    public void Shadowing_DoesNotMixSameTextSymbols()
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
        var service = new VectorReferenceService(new VectorModuleIndex());

        IReadOnlyList<VectorReferenceLocation> inner = service.GetReferences(
            analysis,
            source.IndexOf("print(value)", StringComparison.Ordinal) + "print(".Length + 1,
            [analysis],
            includeDeclaration: true);
        IReadOnlyList<VectorReferenceLocation> outer = service.GetReferences(
            analysis,
            source.LastIndexOf("print(value)", StringComparison.Ordinal) + "print(".Length + 1,
            [analysis],
            includeDeclaration: true);

        Assert.Equal(2, inner.Count);
        Assert.All(inner, location => Assert.Contains(location.Range.Start.Line, new[] { 2, 3 }));
        Assert.Equal(2, outer.Count);
        Assert.All(outer, location => Assert.Contains(location.Range.Start.Line, new[] { 0, 5 }));
    }

    [Fact]
    public void ParametersAndFunctions_UseTheirResolvedIdentities()
    {
        const string source = "function double(value) { return value + value; }\ndouble(21);";
        VectorAnalysisResult analysis = Analyze(source);
        var service = new VectorReferenceService(new VectorModuleIndex());

        IReadOnlyList<VectorReferenceLocation> parameter = service.GetReferences(
            analysis,
            source.IndexOf("return value", StringComparison.Ordinal) + "return ".Length + 1,
            [analysis],
            includeDeclaration: true);
        IReadOnlyList<VectorReferenceLocation> function = service.GetReferences(
            analysis,
            source.LastIndexOf("double", StringComparison.Ordinal) + 1,
            [analysis],
            includeDeclaration: true);

        Assert.Equal(3, parameter.Count);
        Assert.Equal(2, function.Count);
    }

    [Fact]
    public void LocalModuleMember_ReportsOnlyReferencesToTheSameModule()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            WriteModule(root, "alpha", "values", "let answer = 1;\nprint(answer);");
            WriteModule(root, "beta", "values", "let answer = 2;");
            VectorAnalysisResult alphaUse = AnalyzeFile(
                root,
                "alpha-main.vec",
                "import alpha.values;\nalpha.values.answer;");
            VectorAnalysisResult betaUse = AnalyzeFile(
                root,
                "beta-main.vec",
                "import beta.values;\nbeta.values.answer;");
            var service = new VectorReferenceService(new VectorModuleIndex());

            IReadOnlyList<VectorReferenceLocation> references = service.GetReferences(
                alphaUse,
                alphaUse.Document.Text.LastIndexOf("answer", StringComparison.Ordinal) + 1,
                [alphaUse, betaUse],
                includeDeclaration: true);

            Assert.Equal(3, references.Count);
            Assert.DoesNotContain(references, location => location.Uri == betaUse.Document.Uri);
            Assert.Contains(references, location => location.Uri == alphaUse.Document.Uri && !location.IsDeclaration);
            Assert.Contains(references, location => location.IsDeclaration && location.Uri.LocalPath.EndsWith("alpha\\values.vec", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("local")]
    [InlineData("geometry")]
    [InlineData("rectangleArea")]
    public void LocalModuleMember_FindReferencesAcceptsAnyCaretWithinQualifiedExpression(string segment)
    {
        string root = CreateTemporaryDirectory();
        try
        {
            WriteModule(
                root,
                "local",
                "geometry",
                "function rectangleArea(width, height) { return width * height; }");
            const string source = "import local.geometry;\nlocal.geometry.rectangleArea(6, 7);";
            VectorAnalysisResult importer = AnalyzeFile(root, "main.vec", source);
            var service = new VectorReferenceService(new VectorModuleIndex());

            int expressionStart = source.LastIndexOf("local.geometry.rectangleArea", StringComparison.Ordinal);
            int caret = source.IndexOf(segment, expressionStart, StringComparison.Ordinal) + 1;
            IReadOnlyList<VectorReferenceLocation> references = service.GetReferences(
                importer,
                caret,
                [importer],
                includeDeclaration: true);

            Assert.Equal(2, references.Count);
            Assert.Contains(references, location => location.IsDeclaration);
            Assert.Contains(references, location => location.Uri == importer.Document.Uri && !location.IsDeclaration);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UndefinedAndBuiltInNames_ReturnNoReferences()
    {
        const string source = "print(missing);";
        VectorAnalysisResult analysis = Analyze(source);
        var service = new VectorReferenceService(new VectorModuleIndex());

        Assert.Empty(service.GetReferences(analysis, 1, [analysis], includeDeclaration: true));
        Assert.Empty(service.GetReferences(
            analysis,
            source.IndexOf("missing", StringComparison.Ordinal) + 1,
            [analysis],
            includeDeclaration: true));
    }

    [Fact]
    public void UpdatedAnalysis_DoesNotReturnStaleReferences()
    {
        VectorAnalysisResult oldAnalysis = Analyze("let item = 1;\nprint(item);");
        VectorAnalysisResult current = new VectorAnalyzer().Analyze(
            VectorDocumentSnapshot.CreateInMemory("let item = 1;", "references.vec", version: 1));
        var service = new VectorReferenceService(new VectorModuleIndex());

        IReadOnlyList<VectorReferenceLocation> references = service.GetReferences(
            current,
            current.Document.Text.IndexOf("item", StringComparison.Ordinal) + 1,
            [current],
            includeDeclaration: true);

        Assert.Single(references);
        Assert.DoesNotContain(references, location => location.Range.Start.Line == 1);
        Assert.NotEqual(oldAnalysis.Document.Version, current.Document.Version);
    }

    private static VectorAnalysisResult Analyze(string source) =>
        new VectorAnalyzer().Analyze(VectorDocumentSnapshot.CreateInMemory(source, "references.vec"));

    private static VectorAnalysisResult AnalyzeFile(string root, string fileName, string source) =>
        new VectorAnalyzer().Analyze(VectorDocumentSnapshot.FromFile(Path.Combine(root, fileName), source, 0, root));

    private static void WriteModule(string root, string directory, string name, string source)
    {
        string moduleDirectory = Path.Combine(root, directory);
        Directory.CreateDirectory(moduleDirectory);
        File.WriteAllText(Path.Combine(moduleDirectory, name + ".vec"), source);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "vector-references-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
