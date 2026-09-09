namespace Vector.Analysis.Tests;

using Vector.Analysis.Documents;
using Vector.Analysis.Modules;
using Vector.Analysis.Navigation;
using Xunit;

public sealed class VectorRenameTests
{
    [Fact]
    public void LocalVariable_RenamesDeclarationReadsAndWrites()
    {
        const string source = "let value = 1;\nprint(value);\nvalue = value + 1;";
        VectorAnalysisResult analysis = Analyze(source);

        VectorRenameResult result = Service().Rename(
            analysis,
            source.IndexOf("value", StringComparison.Ordinal) + 1,
            "total",
            [analysis]);

        Assert.True(result.Success);
        Assert.Equal(4, result.Edits.Count);
        Assert.All(result.Edits, edit => Assert.Equal("total", edit.NewText));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1value")]
    [InlineData("let")]
    [InlineData("two names")]
    [InlineData("bad-name")]
    public void InvalidIdentifier_FailsAtomically(string newName)
    {
        const string source = "let value = 1;\nprint(value);";
        VectorAnalysisResult analysis = Analyze(source);

        VectorRenameResult result = Service().Rename(analysis, 5, newName, [analysis]);

        Assert.False(result.Success);
        Assert.Equal(VectorRenameFailureCode.InvalidIdentifier, result.FailureCode);
        Assert.Empty(result.Edits);
    }

    [Fact]
    public void SameScopeConflict_FailsWithoutPartialEdits()
    {
        const string source = "let first = 1;\nlet second = first;\nprint(first);";
        VectorAnalysisResult analysis = Analyze(source);

        VectorRenameResult result = Service().Rename(analysis, 5, "second", [analysis]);

        Assert.False(result.Success);
        Assert.Equal(VectorRenameFailureCode.NameConflict, result.FailureCode);
        Assert.Empty(result.Edits);
    }

    [Fact]
    public void Rename_RejectsANameThatWouldRebindReferencesInANestedScope()
    {
        const string source = """
            function calculate(input) {
                if true {
                    let replacement = 1;
                    print(input);
                }
            }
            """;
        VectorAnalysisResult analysis = Analyze(source);

        VectorRenameResult result = Service().Rename(
            analysis,
            source.IndexOf("print(input)", StringComparison.Ordinal) + "print(".Length + 1,
            "replacement",
            [analysis]);

        Assert.False(result.Success);
        Assert.Equal(VectorRenameFailureCode.NameConflict, result.FailureCode);
        Assert.Empty(result.Edits);
    }

    [Fact]
    public void Rename_PreservesUnrelatedShadowedNames()
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

        VectorRenameResult result = Service().Rename(
            analysis,
            source.LastIndexOf("print(value)", StringComparison.Ordinal) + "print(".Length + 1,
            "outerValue",
            [analysis]);

        Assert.True(result.Success);
        Assert.Equal(2, result.Edits.Count);
        Assert.All(result.Edits, edit => Assert.Contains(edit.Range.Start.Line, new[] { 0, 5 }));
    }

    [Fact]
    public void FunctionsAndParameters_AreRenameableWithinResolvedScope()
    {
        const string source = "function double(value) { return value + value; }\ndouble(21);";
        VectorAnalysisResult analysis = Analyze(source);
        VectorRenameService service = Service();

        VectorRenameResult function = service.Rename(
            analysis,
            source.LastIndexOf("double", StringComparison.Ordinal) + 1,
            "twice",
            [analysis]);
        VectorRenameResult parameter = service.Rename(
            analysis,
            source.IndexOf("return value", StringComparison.Ordinal) + "return ".Length + 1,
            "input",
            [analysis]);

        Assert.Equal(2, function.Edits.Count);
        Assert.Equal(3, parameter.Edits.Count);
    }

    [Fact]
    public void ModuleMember_RenamesDeclarationInternalAndQualifiedReferencesAcrossFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "vector-rename-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "local"));
        try
        {
            string modulePath = Path.Combine(root, "local", "values.vec");
            File.WriteAllText(modulePath, "let answer = 42;\nprint(answer);");
            const string source = "import local.values;\nlocal.values.answer;";
            VectorAnalysisResult importer = new VectorAnalyzer().Analyze(VectorDocumentSnapshot.FromFile(
                Path.Combine(root, "main.vec"),
                source,
                0,
                root));

            VectorRenameResult result = Service().Rename(
                importer,
                source.LastIndexOf("answer", StringComparison.Ordinal) + 1,
                "result",
                [importer]);

            Assert.True(result.Success);
            Assert.Equal(3, result.Edits.Count);
            Assert.Equal(2, result.Edits.Count(edit => edit.Uri.LocalPath == Path.GetFullPath(modulePath)));
            Assert.Single(result.Edits, edit => edit.Uri == importer.Document.Uri);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuiltinsStandardMembersAndUndefinedNames_AreNotRenameable()
    {
        const string source = "import lib.math;\nprint(missing);\nlib.math.sqrt(9);";
        VectorAnalysisResult analysis = Analyze(source);
        VectorRenameService service = Service();

        foreach (int offset in new[]
        {
            source.IndexOf("print", StringComparison.Ordinal) + 1,
            source.IndexOf("missing", StringComparison.Ordinal) + 1,
            source.IndexOf("sqrt", StringComparison.Ordinal) + 1,
        })
        {
            VectorRenameResult result = service.Rename(analysis, offset, "renamed", [analysis]);
            Assert.False(result.Success);
            Assert.Equal(VectorRenameFailureCode.NotRenameable, result.FailureCode);
            Assert.Empty(result.Edits);
        }
    }

    private static VectorRenameService Service() => new(new VectorModuleIndex());

    private static VectorAnalysisResult Analyze(string source) =>
        new VectorAnalyzer().Analyze(VectorDocumentSnapshot.CreateInMemory(source, "rename.vec"));
}
