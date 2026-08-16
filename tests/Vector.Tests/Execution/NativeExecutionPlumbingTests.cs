using Vector.Cli;
using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;
using Xunit;

namespace Vector.Tests.Execution;

public sealed class NativeExecutionPlumbingTests
{
    [Fact]
    public void VectorEngineExecutesWithInjectedNativeRegistry()
    {
        var registry = CreateRegistry();
        var engine = new VectorEngine(registry);

        var result = engine.Execute(
            "import test.native; test.native.double(test.native.answer);");

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(84), result.Result);
        Assert.Same(registry, engine.NativeModules);
    }

    [Fact]
    public void VectorEngineWithoutCustomRegistryStillExecutesOrdinaryVectorCode()
    {
        var result = new VectorEngine().Execute("let value = 6; value * 7;");

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(42), result.Result);
    }

    [Fact]
    public void VectorEngineInjectedNativeModuleCoexistsWithSourceModule()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("local", "let value = 8;");
        var engine = new VectorEngine(CreateRegistry());

        var result = engine.Execute(
            "import local; import test.native; local.value + test.native.answer;",
            program.Root);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(50), result.Result);
    }

    [Fact]
    public void VectorEngineReportsNativeSourceConflictWithStableDiagnostic()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("test.native", "let sourceValue = 1;");
        var engine = new VectorEngine(CreateRegistry());

        var result = engine.Execute("import test.native;", program.Root);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ModuleConflict, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void VectorEngineReportsNativeInitializationFailureWithStableDiagnostic()
    {
        var registry = new NativeModuleRegistry();
        registry.Register(new NativeModuleDefinition(
            Id("test.broken"),
            _ => throw new InvalidOperationException("SECRET HOST DETAIL")));
        var engine = new VectorEngine(registry);

        var result = engine.Execute("import test.broken;");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.DoesNotContain("SECRET HOST DETAIL", diagnostic.Message);
    }

    [Fact]
    public void ReplCanImportInjectedNativeModule()
    {
        var session = RunRepl(
            "import test.native;\ntest.native.double(5);\n:exit\n",
            CreateRegistry());

        Assert.Contains("10", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplNativeModuleInitializationPersistsAcrossSubmissions()
    {
        var initializationCount = 0;
        var registry = new NativeModuleRegistry();
        registry.Register(new NativeModuleDefinition(Id("test.once"), context =>
        {
            initializationCount++;
            context.Export("answer", new NumberValue(42));
        }));

        var session = RunRepl(
            "import test.once;\nimport test.once;\ntest.once.answer;\n:exit\n",
            registry);

        Assert.Equal(1, initializationCount);
        Assert.Contains("42", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplReportsInjectedNativeInitializationFailureAndContinues()
    {
        var registry = new NativeModuleRegistry();
        registry.Register(new NativeModuleDefinition(
            Id("test.broken"),
            _ => throw new InvalidOperationException("SECRET HOST DETAIL")));

        var session = RunRepl(
            "import test.broken;\n1 + 1;\n:exit\n",
            registry);

        Assert.Contains("NativeRuntimeFailure", session.Error);
        Assert.DoesNotContain("SECRET HOST DETAIL", session.Error);
        Assert.Contains("2", session.Output);
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

    private static ReplSession RunRepl(string input, NativeModuleRegistry registry)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var repl = new Vector.Cli.Repl(
            new StringReader(input),
            output,
            error,
            nativeModules: registry);

        var exitCode = repl.Run();

        Assert.Equal(0, exitCode);
        return new ReplSession(output.ToString(), error.ToString());
    }

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private sealed record ReplSession(string Output, string Error);

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorExecutionPlumbing-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void WriteModule(string qualifiedName, string source)
        {
            var relativePath = qualifiedName.Replace('.', Path.DirectorySeparatorChar) + ".vec";
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Best-effort test cleanup.
            }
        }
    }
}
