using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Modules;

public sealed class NativeModuleRegistryTests
{
    [Fact]
    public void RegistersDefinitionByQualifiedModuleId()
    {
        var registry = new NativeModuleRegistry();
        var definition = Definition("lib.math");

        registry.Register(definition);

        Assert.Single(registry.Definitions);
        Assert.Same(definition, Assert.Single(registry.Definitions));
        Assert.Equal("lib.math", definition.QualifiedNamespace);
    }

    [Fact]
    public void LookupReturnsRegisteredDefinitionForEquivalentModuleId()
    {
        var registry = new NativeModuleRegistry();
        var definition = Definition("lib.math");
        registry.Register(definition);

        var found = registry.TryGet(Id("lib.math"), out var resolved);

        Assert.True(found);
        Assert.Same(definition, resolved);
    }

    [Fact]
    public void MissingLookupReturnsFalseAndNullDefinition()
    {
        var registry = new NativeModuleRegistry();

        var found = registry.TryGet(Id("lib.missing"), out var resolved);

        Assert.False(found);
        Assert.Null(resolved);
    }

    [Fact]
    public void DuplicateRegistrationForSameQualifiedModuleIdIsRejected()
    {
        var registry = new NativeModuleRegistry();
        registry.Register(Definition("lib.math"));

        var error = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(Definition("lib.math")));

        Assert.Contains("lib.math", error.Message);
        Assert.Single(registry.Definitions);
    }

    [Fact]
    public void FullQualifiedModulePathsRemainDistinct()
    {
        var registry = new NativeModuleRegistry();
        var gameGeometry = Definition("game.geometry");
        var mathGeometry = Definition("math.geometry");

        registry.Register(gameGeometry);
        registry.Register(mathGeometry);

        Assert.True(registry.TryGet(Id("game.geometry"), out var resolvedGame));
        Assert.True(registry.TryGet(Id("math.geometry"), out var resolvedMath));
        Assert.Same(gameGeometry, resolvedGame);
        Assert.Same(mathGeometry, resolvedMath);
        Assert.Equal(2, registry.Definitions.Count);
    }

    [Fact]
    public void DefinitionExportsNamedVectorValuesIntoProvidedEnvironment()
    {
        var definition = new NativeModuleDefinition(
            Id("test.native"),
            context =>
            {
                context.Export("answer", new NumberValue(42));
                context.Export("ready", new BooleanValue(true));
            });
        var environment = new RuntimeEnvironment();

        definition.Initialize(environment);

        Assert.Equal(new NumberValue(42), environment.Get("answer", Span()));
        Assert.Equal(new BooleanValue(true), environment.Get("ready", Span()));
    }

    [Fact]
    public void NativeModuleContextRejectsEmptyExportNames()
    {
        var definition = new NativeModuleDefinition(
            Id("test.native"),
            context => context.Export(" ", new NumberValue(1)));

        Assert.Throws<ArgumentException>(() => definition.Initialize(new RuntimeEnvironment()));
    }

    private static NativeModuleDefinition Definition(string qualifiedName) =>
        new(Id(qualifiedName), _ => { });

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private static SourceSpan Span() =>
        new(new SourcePosition(0, 1, 1), new SourcePosition(0, 1, 1));
}
