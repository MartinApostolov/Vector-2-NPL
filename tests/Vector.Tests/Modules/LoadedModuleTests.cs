using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Parsing;
using Vector.Core.Source;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Modules;

public sealed class LoadedModuleTests
{
    [Fact]
    public void SourceModuleKeepsCommonIdentityEnvironmentAndImports()
    {
        var id = Id("app.feature");
        var dependency = Id("lib.geometry");
        var environment = new RuntimeEnvironment();
        var module = new LoadedModule(
            id,
            SourceData("feature.vec", "import lib.geometry;"),
            environment,
            new[] { dependency });

        Assert.Equal(id, module.Id);
        Assert.Equal("app.feature", module.QualifiedNamespace);
        Assert.Same(environment, module.Environment);
        Assert.Equal(new[] { dependency }, module.Imports);
    }

    [Fact]
    public void SourceModulePreservesParsedSourceMetadata()
    {
        const string source = "let answer = 42;";
        var sourceData = SourceData("answer.vec", source);
        var module = new LoadedModule(
            Id("answer"),
            sourceData,
            new RuntimeEnvironment(),
            Array.Empty<ModuleId>());

        Assert.Same(sourceData, module.SourceData);
        Assert.Equal(Path.GetFullPath("answer.vec"), module.FilePath);
        Assert.Equal(source, module.Source);
        Assert.Same(sourceData.Syntax, module.Syntax);
        Assert.Single(module.Syntax!.Statements);
    }

    [Fact]
    public void SourceModuleReportsSourceKindWithoutNativeDefinition()
    {
        var module = SourceModule("lib.geometry");

        Assert.Equal(ModuleKind.Source, module.Kind);
        Assert.Null(module.NativeDefinition);
        Assert.NotNull(module.SourceData);
    }

    [Fact]
    public void SourceModuleDataNormalizesFilePath()
    {
        var sourceData = SourceData(Path.Combine("relative", "module.vec"), "let value = 1;");

        Assert.True(Path.IsPathFullyQualified(sourceData.FilePath));
        Assert.Equal(Path.GetFullPath(Path.Combine("relative", "module.vec")), sourceData.FilePath);
    }

    [Fact]
    public void NativeModuleKeepsCommonIdentityAndEnvironment()
    {
        var definition = Definition("lib.math");
        var environment = new RuntimeEnvironment();
        var module = new LoadedModule(definition, environment);

        Assert.Equal(definition.Id, module.Id);
        Assert.Equal("lib.math", module.QualifiedNamespace);
        Assert.Same(environment, module.Environment);
    }

    [Fact]
    public void NativeModuleReportsNativeKindAndRetainsDefinition()
    {
        var definition = Definition("lib.math");
        var module = new LoadedModule(definition, new RuntimeEnvironment());

        Assert.Equal(ModuleKind.Native, module.Kind);
        Assert.Same(definition, module.NativeDefinition);
    }

    [Fact]
    public void NativeModuleRequiresNoFakeSourceMetadata()
    {
        var module = new LoadedModule(Definition("lib.math"), new RuntimeEnvironment());

        Assert.Null(module.SourceData);
        Assert.Null(module.FilePath);
        Assert.Null(module.Source);
        Assert.Null(module.Syntax);
    }

    [Fact]
    public void NativeModuleStartsWithoutSourceDeclaredImports()
    {
        var module = new LoadedModule(Definition("lib.math"), new RuntimeEnvironment());

        Assert.Empty(module.Imports);
    }

    private static LoadedModule SourceModule(string qualifiedName) =>
        new(
            Id(qualifiedName),
            SourceData("module.vec", "let value = 1;"),
            new RuntimeEnvironment(),
            Array.Empty<ModuleId>());

    private static SourceModuleData SourceData(string filePath, string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return new SourceModuleData(filePath, source, parseResult.Root);
    }

    private static NativeModuleDefinition Definition(string qualifiedName) =>
        new(Id(qualifiedName), _ => { });

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));
}
