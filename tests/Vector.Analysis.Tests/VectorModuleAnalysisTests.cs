namespace Vector.Analysis.Tests;

using Vector.Analysis.Documents;
using Vector.Analysis.IntelliSense;
using Vector.Analysis.Modules;
using Vector.Analysis.Workspace;
using Xunit;

public sealed class VectorModuleAnalysisTests
{
    [Fact]
    public void NestedModulePath_OffersOnlyTopLevelQualifiedMembers()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib/geometry.vec", "let origin = [0, 0];\nfunction distance(a, b) { return 0; }");
        VectorAnalysisResult main = program.AnalyzeMain("import lib.geometry;\nlib.geometry.");

        IReadOnlyList<VectorCatalogItem> items = Complete(main, new VectorModuleIndex());

        Assert.Equal(["distance", "origin"], items.Select(item => item.Label));
        Assert.Contains(items, item => item.Label == "distance" && item.Detail == "distance(a, b)");
        Assert.Contains(items, item => item.Label == "origin" && item.Kind == VectorCatalogItemKind.Variable);
    }

    [Fact]
    public void MissingOrUnimportedModule_HasNoCompletionAndNoShortAlias()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib/geometry.vec", "let origin = [0, 0];");
        var modules = new VectorModuleIndex();

        VectorAnalysisResult missing = program.AnalyzeMain("import local.missing;\nlocal.missing.");
        VectorAnalysisResult unimported = program.AnalyzeMain("lib.geometry.");
        VectorAnalysisResult shortAlias = program.AnalyzeMain("import lib.geometry;\ngeometry.");

        Assert.Empty(Complete(missing, modules));
        Assert.Empty(Complete(unimported, modules));
        Assert.Empty(Complete(shortAlias, modules));
    }

    [Fact]
    public void SeparateProgramRoots_DoNotShareModulesWithTheSameIdentity()
    {
        using var first = new TemporaryProgram();
        using var second = new TemporaryProgram();
        first.WriteModule("shared/tools.vec", "let firstOnly = 1;");
        second.WriteModule("shared/tools.vec", "let secondOnly = 2;");
        var modules = new VectorModuleIndex();

        IReadOnlyList<VectorCatalogItem> firstItems = Complete(
            first.AnalyzeMain("import shared.tools;\nshared.tools."),
            modules);
        IReadOnlyList<VectorCatalogItem> secondItems = Complete(
            second.AnalyzeMain("import shared.tools;\nshared.tools."),
            modules);

        Assert.Equal("firstOnly", Assert.Single(firstItems).Label);
        Assert.Equal("secondOnly", Assert.Single(secondItems).Label);
    }

    [Fact]
    public void DiskEdit_ReplacesCachedMembersEvenWhenFileLengthIsUnchanged()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("feature.vec", "let before = 1;");
        VectorAnalysisResult main = program.AnalyzeMain("import feature;\nfeature.");
        var modules = new VectorModuleIndex();

        Assert.Equal("before", Assert.Single(Complete(main, modules)).Label);
        program.WriteModule("feature.vec", "let after_ = 2;");

        Assert.Equal("after_", Assert.Single(Complete(main, modules)).Label);
    }

    [Fact]
    public void DeletedModule_RemovesCachedMembers()
    {
        using var program = new TemporaryProgram();
        string modulePath = program.WriteModule("feature.vec", "let available = 1;");
        VectorAnalysisResult main = program.AnalyzeMain("import feature;\nfeature.");
        var modules = new VectorModuleIndex();
        Assert.NotEmpty(Complete(main, modules));

        File.Delete(modulePath);

        Assert.Empty(Complete(main, modules));
    }

    [Fact]
    public void OpenModuleEdit_OverridesDiskAndClosingFallsBackToCurrentDisk()
    {
        using var program = new TemporaryProgram();
        string modulePath = program.WriteModule("feature.vec", "let onDisk = 1;");
        VectorAnalysisResult main = program.AnalyzeMain("import feature;\nfeature.");
        var workspace = new VectorWorkspace();
        Assert.True(workspace.TryUpdateDocument(main.Document, out VectorAnalysisResult trackedMain));
        Assert.Equal("onDisk", Assert.Single(Complete(trackedMain, workspace.Modules)).Label);

        VectorDocumentSnapshot edited = VectorDocumentSnapshot.FromFile(
            modulePath,
            "let unsaved = 2;",
            1,
            program.Root);
        Assert.True(workspace.TryUpdateDocument(edited, out _));
        Assert.Equal("unsaved", Assert.Single(Complete(trackedMain, workspace.Modules)).Label);

        Assert.True(workspace.RemoveDocument(edited.Uri));
        Assert.Equal("onDisk", Assert.Single(Complete(trackedMain, workspace.Modules)).Label);
    }

    [Fact]
    public void ImportedDocument_InheritsTheImportingProgramRoot()
    {
        using var program = new TemporaryProgram();
        string modulePath = program.WriteModule("nested/tools.vec", "let value = 1;");
        VectorAnalysisResult main = program.AnalyzeMain("import nested.tools;\nnested.tools.");
        var modules = new VectorModuleIndex();

        Assert.NotEmpty(Complete(main, modules));

        Assert.Equal(Path.GetFullPath(program.Root), modules.GetInheritedProgramRoot(modulePath));
    }

    [Fact]
    public void ModuleAnalysis_DoesNotExecuteTopLevelHostOperations()
    {
        using var program = new TemporaryProgram();
        program.WriteModule(
            "interactive.vec",
            "import lib.io;\nlet captured = lib.io.readLine();\nprint(captured);");
        VectorAnalysisResult main = program.AnalyzeMain("import interactive;\ninteractive.");

        IReadOnlyList<VectorCatalogItem> items = Complete(main, new VectorModuleIndex());

        Assert.Equal("captured", Assert.Single(items).Label);
        Assert.DoesNotContain(
            typeof(VectorModuleIndex).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "Vector.Plugins");
    }

    private static IReadOnlyList<VectorCatalogItem> Complete(
        VectorAnalysisResult analysis,
        VectorModuleIndex modules) => new VectorCompletionService(modules)
            .GetCompletions(analysis, analysis.Document.Text.Length);

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            this.Root = Path.Combine(Path.GetTempPath(), "vector-analysis-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Root);
        }

        public string Root { get; }

        public VectorAnalysisResult AnalyzeMain(string source)
        {
            string mainPath = Path.Combine(this.Root, "main.vec");
            return new VectorAnalyzer().Analyze(VectorDocumentSnapshot.FromFile(mainPath, source, 0));
        }

        public string WriteModule(string relativePath, string source)
        {
            string path = Path.Combine(this.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(this.Root))
            {
                Directory.Delete(this.Root, recursive: true);
            }
        }
    }
}
