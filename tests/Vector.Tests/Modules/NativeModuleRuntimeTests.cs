using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Modules;

public sealed class NativeModuleRuntimeTests
{
    [Fact]
    public void LoadReturnsRegisteredNativeModuleWithoutSourceFile()
    {
        using var program = new TemporaryProgram();
        var registry = CreateRegistry();
        var loader = program.CreateLoader(registry);

        var module = loader.Load(Id("test.native"));

        Assert.Equal(ModuleKind.Native, module.Kind);
        Assert.Equal("test.native", module.QualifiedNamespace);
        Assert.Null(module.SourceData);
        Assert.Single(loader.LoadedModules);
    }

    [Fact]
    public void ImportInitializesNativeExportsAndAllowsNormalQualifiedCalls()
    {
        using var program = new TemporaryProgram();
        var loader = program.CreateLoader(CreateRegistry());

        var interpreter = Execute(loader, "import test.native; let value = test.native.double(5); let answer = test.native.answer;");

        Assert.Equal(new NumberValue(10), Get(interpreter, "value"));
        Assert.Equal(new NumberValue(42), Get(interpreter, "answer"));
        Assert.True(loader.IsInitialized(Id("test.native")));
    }

    [Fact]
    public void RepeatedNativeImportInitializesExactlyOnce()
    {
        using var program = new TemporaryProgram();
        var initializationCount = 0;
        var registry = new NativeModuleRegistry();
        registry.Register(new NativeModuleDefinition(Id("test.once"), context =>
        {
            initializationCount++;
            context.Export("value", new NumberValue(1));
        }));
        var loader = program.CreateLoader(registry);
        var host = new VectorHost();

        var first = loader.Import(Id("test.once"), host);
        var second = loader.Import(Id("test.once"), host);

        Assert.Same(first, second);
        Assert.Equal(1, initializationCount);
        Assert.Single(loader.InitializedModules);
    }

    [Fact]
    public void NativeAndSourceModulesCoexistInOneProgram()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("local", "let value = 8;");
        var loader = program.CreateLoader(CreateRegistry());

        var interpreter = Execute(
            loader,
            "import local; import test.native; let combined = local.value + test.native.answer;");

        Assert.Equal(new NumberValue(50), Get(interpreter, "combined"));
        Assert.Equal(2, loader.LoadedModules.Count);
    }

    [Fact]
    public void SourceModuleCanImportAndUseNativeModule()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("wrapper", "import test.native; let result = test.native.double(6);");
        var loader = program.CreateLoader(CreateRegistry());

        var interpreter = Execute(loader, "import wrapper; let result = wrapper.result;");

        Assert.Equal(new NumberValue(12), Get(interpreter, "result"));
        Assert.True(loader.IsInitialized(Id("wrapper")));
        Assert.True(loader.IsInitialized(Id("test.native")));
    }

    [Fact]
    public void SameMemberNamesRemainIsolatedAcrossNativeModules()
    {
        using var program = new TemporaryProgram();
        var registry = new NativeModuleRegistry();
        registry.Register(ValueModule("first.native", 10));
        registry.Register(ValueModule("second.native", 20));
        var loader = program.CreateLoader(registry);

        var interpreter = Execute(
            loader,
            "import first.native; import second.native; let total = first.native.value + second.native.value;");

        Assert.Equal(new NumberValue(30), Get(interpreter, "total"));
    }

    [Fact]
    public void LoadedNativeModuleStillRequiresExplicitImportForAccess()
    {
        using var program = new TemporaryProgram();
        var loader = program.CreateLoader(CreateRegistry());
        loader.Load(Id("test.native"));

        var error = Assert.Throws<RuntimeError>(() => Execute(loader, "let value = test.native.answer;"));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
    }

    [Fact]
    public void ShortenedNativeModulePathIsNotAccepted()
    {
        using var program = new TemporaryProgram();
        var loader = program.CreateLoader(CreateRegistry());

        var error = Assert.Throws<RuntimeError>(() =>
            Execute(loader, "import test.native; let value = native.answer;"));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
    }

    [Fact]
    public void SourceAndNativeModuleWithSameIdProduceExplicitConflict()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("test.native", "let sourceValue = 1;");
        var loader = program.CreateLoader(CreateRegistry());

        var error = Assert.Throws<ModuleLoadException>(() => loader.Load(Id("test.native")));

        Assert.Equal(ModuleLoadErrorKind.ModuleConflict, error.Kind);
        Assert.Contains("both a local Vector source file and a registered native module", error.Message);
        Assert.Empty(loader.LoadedModules);
    }

    [Fact]
    public void NativeModuleLoadIsCachedBeforeInitialization()
    {
        using var program = new TemporaryProgram();
        var loader = program.CreateLoader(CreateRegistry());

        var first = loader.Load(Id("test.native"));
        var second = loader.Load(Id("test.native"));

        Assert.Same(first, second);
        Assert.False(loader.IsInitialized(Id("test.native")));
    }

    [Fact]
    public void UnexpectedNativeInitializerExceptionBecomesSafeModuleFailure()
    {
        using var program = new TemporaryProgram();
        var registry = new NativeModuleRegistry();
        registry.Register(new NativeModuleDefinition(
            Id("test.broken"),
            _ => throw new InvalidOperationException("SECRET HOST DETAIL")));
        var loader = program.CreateLoader(registry);

        var error = Assert.Throws<ModuleLoadException>(() =>
            loader.Import(Id("test.broken"), new VectorHost()));

        Assert.Equal(ModuleLoadErrorKind.NativeInitializationFailure, error.Kind);
        Assert.Equal("Native module 'test.broken' failed to initialize.", error.Message);
        Assert.DoesNotContain("SECRET HOST DETAIL", error.Message);
        Assert.DoesNotContain(" at ", error.Message);
        Assert.False(loader.IsInitialized(Id("test.broken")));
    }

    [Fact]
    public void DeliberateNativeInitializerFailureRetainsSafeVectorFacingMessage()
    {
        using var program = new TemporaryProgram();
        var registry = new NativeModuleRegistry();
        registry.Register(new NativeModuleDefinition(
            Id("test.rejected"),
            _ => throw new NativeRuntimeException(
                DiagnosticCode.RuntimeTypeError,
                "Native setup rejected configuration.")));
        var loader = program.CreateLoader(registry);

        var error = Assert.Throws<ModuleLoadException>(() =>
            loader.Import(Id("test.rejected"), new VectorHost()));

        Assert.Equal(ModuleLoadErrorKind.NativeInitializationFailure, error.Kind);
        Assert.Contains("Native setup rejected configuration.", error.Message);
    }

    private static NativeModuleRegistry CreateRegistry()
    {
        var registry = new NativeModuleRegistry();
        registry.Register(new NativeModuleDefinition(Id("test.native"), context =>
        {
            context.Export("answer", new NumberValue(42));
            context.Export(
                "double",
                new NativeFunction(
                    "double",
                    1,
                    (_, arguments) => NativeValueConverter.FromNumber(
                        NativeValueConverter.ToNumber(arguments[0], "value") * 2)));
        }));
        return registry;
    }

    private static NativeModuleDefinition ValueModule(string qualifiedName, double value) =>
        new(Id(qualifiedName), context => context.Export("value", new NumberValue(value)));

    private static Interpreter Execute(ModuleLoader loader, string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);

        var interpreter = new Interpreter(moduleLoader: loader);
        interpreter.Execute(parseResult.Root, sourceName: null, sourceText: source);
        return interpreter;
    }

    private static VectorValue Get(Interpreter interpreter, string name) =>
        interpreter.CurrentEnvironment.Get(name, Span());

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private static SourceSpan Span() =>
        new(new SourcePosition(0, 1, 1), new SourcePosition(0, 1, 1));

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorNativeModuleTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public ModuleLoader CreateLoader(NativeModuleRegistry registry) =>
            new(new ModuleResolver(Root), registry);

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
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
