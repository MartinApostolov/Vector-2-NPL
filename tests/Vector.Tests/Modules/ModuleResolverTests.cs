using Vector.Core.Modules;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Modules;

public sealed class ModuleResolverTests
{
    [Fact]
    public void ModuleIdPreservesFullQualifiedNameAndSegments()
    {
        var id = new ModuleId(new[] { "lib", "geometry" });

        Assert.Equal("lib.geometry", id.QualifiedName);
        Assert.Equal(new[] { "lib", "geometry" }, id.Segments);
        Assert.Equal("lib.geometry", id.ToString());
    }

    [Fact]
    public void ModuleIdSupportsUnicodeIdentifiers()
    {
        var id = new ModuleId(new[] { "библиотека", "геометрия" });

        Assert.Equal("библиотека.геометрия", id.QualifiedName);
    }

    [Fact]
    public void EqualQualifiedModuleIdsCompareEqual()
    {
        var first = new ModuleId(new[] { "lib", "geometry" });
        var second = new ModuleId(new[] { "lib", "geometry" });

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1bad")]
    [InlineData("two.words")]
    [InlineData("bad/name")]
    [InlineData("if")]
    public void ModuleIdRejectsInvalidOrReservedPathSegments(string segment)
    {
        Assert.Throws<ArgumentException>(() => new ModuleId(new[] { "lib", segment }));
    }

    [Fact]
    public void ModuleIdRejectsEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => new ModuleId(Array.Empty<string>()));
    }

    [Fact]
    public void ResolverMapsQualifiedNameToNestedVecPathUnderProgramRoot()
    {
        using var program = new TemporaryProgram();
        var resolver = new ModuleResolver(program.Root);

        var path = resolver.Resolve(new ModuleId(new[] { "lib", "geometry" }));

        Assert.Equal(
            Path.GetFullPath(Path.Combine(program.Root, "lib", "geometry.vec")),
            path);
    }

    [Fact]
    public void ResolverMapsSingleSegmentModuleToProgramRoot()
    {
        using var program = new TemporaryProgram();
        var resolver = new ModuleResolver(program.Root);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(program.Root, "utilities.vec")),
            resolver.Resolve(new ModuleId(new[] { "utilities" })));
    }

    [Fact]
    public void ResolverNormalizesProgramRootToAbsolutePath()
    {
        var relative = Path.Combine(".", "some", "program");
        var resolver = new ModuleResolver(relative);

        Assert.True(Path.IsPathFullyQualified(resolver.ProgramRoot));
        Assert.Equal(Path.GetFullPath(relative), resolver.ProgramRoot);
    }

    [Fact]
    public void LoaderParsesModuleAndPreservesQualifiedNamespace()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.geometry", "let pi = 3.14;");
        var loader = program.CreateLoader();

        var module = loader.Load(Id("lib.geometry"));

        Assert.Equal("lib.geometry", module.QualifiedNamespace);
        Assert.Equal(Id("lib.geometry"), module.Id);
        Assert.Equal(ModuleKind.Source, module.Kind);
        Assert.NotNull(module.SourceData);
        Assert.Single(module.Syntax!.Statements);
        Assert.Equal(Path.Combine(program.Root, "lib", "geometry.vec"), module.FilePath);
    }

    [Fact]
    public void LoaderRecursivelyLoadsDeclaredImports()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("app.main", "import lib.geometry; let ready = true;");
        program.WriteModule("lib.geometry", "let pi = 3.14;");
        var loader = program.CreateLoader();

        var entry = loader.Load(Id("app.main"));

        Assert.Equal(new[] { Id("lib.geometry") }, entry.Imports);
        Assert.True(loader.TryGetLoaded(Id("lib.geometry"), out var dependency));
        Assert.NotNull(dependency);
        Assert.Equal(2, loader.LoadedModules.Count);
    }

    [Fact]
    public void LoaderCachesAndReturnsSameLoadedModuleInstance()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.geometry", "let value = 1;");
        var loader = program.CreateLoader();
        var id = Id("lib.geometry");

        var first = loader.Load(id);
        program.WriteModule("lib.geometry", "let value = 999;");
        var second = loader.Load(new ModuleId(new[] { "lib", "geometry" }));

        Assert.Same(first, second);
        Assert.Single(loader.LoadedModules);
        Assert.Single(second.Syntax!.Statements);
    }

    [Fact]
    public void EachLoadedModuleOwnsAnIndependentTopLevelEnvironment()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("first", "let value = 1;");
        program.WriteModule("second", "let value = 2;");
        var loader = program.CreateLoader();

        var first = loader.Load(Id("first"));
        var second = loader.Load(Id("second"));

        Assert.NotSame(first.Environment, second.Environment);
        Assert.Null(first.Environment.Enclosing);
        Assert.Null(second.Environment.Enclosing);

        first.Environment.Declare("onlyFirst", new NumberValue(1), Span());
        Assert.Equal(new NumberValue(1), first.Environment.Get("onlyFirst", Span()));
        Assert.Throws<RuntimeError>(() => second.Environment.Get("onlyFirst", Span()));
    }

    [Fact]
    public void LoadingDoesNotExecuteModuleTopLevelCodeYet()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("data", "let answer = 42;");
        var module = program.CreateLoader().Load(Id("data"));

        Assert.Throws<RuntimeError>(() => module.Environment.Get("answer", Span()));
    }

    [Fact]
    public void MissingModuleProducesStructuredModuleLoadError()
    {
        using var program = new TemporaryProgram();
        var loader = program.CreateLoader();
        var id = Id("missing.module");

        var error = Assert.Throws<ModuleLoadException>(() => loader.Load(id));

        Assert.Equal(ModuleLoadErrorKind.ModuleNotFound, error.Kind);
        Assert.Equal(id, error.ModuleId);
        Assert.EndsWith(Path.Combine("missing", "module.vec"), error.FilePath);
        Assert.Contains("missing.module", error.Message);
    }

    [Fact]
    public void SyntaxErrorsProduceStructuredModuleLoadErrorWithParserDiagnostics()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("broken", "let value = ;");
        var loader = program.CreateLoader();

        var error = Assert.Throws<ModuleLoadException>(() => loader.Load(Id("broken")));

        Assert.Equal(ModuleLoadErrorKind.InvalidSyntax, error.Kind);
        Assert.NotEmpty(error.Diagnostics);
        Assert.All(error.Diagnostics, diagnostic => Assert.True(diagnostic.Span.Length >= 0));
        Assert.Empty(loader.LoadedModules);
    }

    [Fact]
    public void SelfImportIsReportedAsCircularImport()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("loop", "import loop;");
        var loader = program.CreateLoader();

        var error = Assert.Throws<ModuleLoadException>(() => loader.Load(Id("loop")));

        Assert.Equal(ModuleLoadErrorKind.CircularImport, error.Kind);
        Assert.Equal(new[] { Id("loop"), Id("loop") }, error.Cycle);
        Assert.Contains("loop -> loop", error.Message);
        Assert.Empty(loader.LoadedModules);
    }

    [Fact]
    public void MultiModuleCycleReportsQualifiedCycleChain()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("a", "import b;");
        program.WriteModule("b", "import c;");
        program.WriteModule("c", "import a;");
        var loader = program.CreateLoader();

        var error = Assert.Throws<ModuleLoadException>(() => loader.Load(Id("a")));

        Assert.Equal(ModuleLoadErrorKind.CircularImport, error.Kind);
        Assert.Equal(new[] { Id("a"), Id("b"), Id("c"), Id("a") }, error.Cycle);
        Assert.Contains("a -> b -> c -> a", error.Message);
        Assert.Empty(loader.LoadedModules);
    }

    [Fact]
    public void FailedLoadCleansLoadingStateAndMayBeRetriedAfterFileAppears()
    {
        using var program = new TemporaryProgram();
        var loader = program.CreateLoader();
        var id = Id("later");

        Assert.Throws<ModuleLoadException>(() => loader.Load(id));
        program.WriteModule("later", "let exists = true;");

        var loaded = loader.Load(id);

        Assert.Equal(id, loaded.Id);
        Assert.Single(loader.LoadedModules);
    }

    [Fact]
    public void SharedDependencyIsLoadedOnlyOnceAcrossImportGraph()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("app", "import left; import right;");
        program.WriteModule("left", "import shared;");
        program.WriteModule("right", "import shared;");
        program.WriteModule("shared", "let value = 1;");
        var loader = program.CreateLoader();

        loader.Load(Id("app"));

        Assert.Equal(4, loader.LoadedModules.Count);
        Assert.True(loader.TryGetLoaded(Id("shared"), out var shared));
        Assert.Same(shared, loader.Load(Id("shared")));
    }

    [Fact]
    public void FullQualifiedPathsRemainDistinctWhenFinalSegmentMatches()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("game.geometry", "let kind = 1;");
        program.WriteModule("math.geometry", "let kind = 2;");
        var loader = program.CreateLoader();

        var game = loader.Load(Id("game.geometry"));
        var math = loader.Load(Id("math.geometry"));

        Assert.NotEqual(game.Id, math.Id);
        Assert.Equal("game.geometry", game.QualifiedNamespace);
        Assert.Equal("math.geometry", math.QualifiedNamespace);
        Assert.Equal(2, loader.LoadedModules.Count);
    }

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private static SourceSpan Span() =>
        new(new SourcePosition(0, 1, 1), new SourcePosition(0, 1, 1));

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorModuleTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public ModuleLoader CreateLoader() => new(new ModuleResolver(Root));

        public void WriteModule(string qualifiedName, string source)
        {
            var segments = qualifiedName.Split('.');
            var fileName = segments[^1] + ".vec";
            var directory = segments.Length == 1
                ? Root
                : Path.Combine(new[] { Root }.Concat(segments[..^1]).ToArray());

            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), source);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
                // Tests should not fail solely because a test runner briefly retains a file handle.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
