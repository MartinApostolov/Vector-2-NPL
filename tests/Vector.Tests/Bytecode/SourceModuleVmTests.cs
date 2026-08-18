using Vector.Core.Bytecode.Compiler;
using Vector.Core.Bytecode.Vm;
using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Vector.Core.StandardLibrary;
using Vector.Core.Syntax;
using Vector.Plugins;
using Xunit;

namespace Vector.Tests.Bytecode;

public sealed class SourceModuleVmTests
{
    [Fact]
    public void VmInitializesSourceModuleIntoPersistentEnvironmentAndCallsItsFunction()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule(
            "local.math",
            "let base = 10; function addBase(value) { return base + value; }");
        var loader = program.CreateVmLoader();

        var result = ExecuteVm(
            "import local.math; local.math.addBase(5);",
            loader,
            program.Host,
            program.MainPath);

        Assert.Equal(new NumberValue(15), result);
        var module = loader.Load(Id("local.math"));
        Assert.Equal(new NumberValue(10), module.Environment.Get("base", Span()));
        Assert.IsType<BytecodeFunctionValue>(module.Environment.Get("addBase", Span()));
    }

    [Fact]
    public void SourceDependenciesInitializeInOrderAndSharedDependencyRunsOnce()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule("shared", "print(\"shared\"); let value = 3;");
        program.WriteModule(
            "left",
            "import shared; print(\"left\"); function value() { return shared.value; }");
        program.WriteModule(
            "right",
            "import shared; print(\"right\"); function value() { return shared.value; }");
        var loader = program.CreateVmLoader();

        var result = ExecuteVm(
            "import left; import right; left.value() + right.value();",
            loader,
            program.Host,
            program.MainPath);

        Assert.Equal(new NumberValue(6), result);
        Assert.Equal(new[] { "shared", "left", "right" }, program.Output);
        Assert.Equal(3, loader.InitializedModules.Count);
        Assert.Single(loader.InitializedModules, module => module.Id.Equals(Id("shared")));
    }

    [Fact]
    public void RepresentativeMixedSourceModuleGraphReturnsFortyTwoFortyThree()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule("shared", "let base = 40;");
        program.WriteModule(
            "feature",
            "import shared; import lib.math; " +
            "let offset = 0; " +
            "function next() { offset = offset + 1; return shared.base + offset + lib.math.sqrt(1); }");

        AssertBackendsMatch(
            program,
            "import feature; [feature.next(), feature.next()];",
            new ListValue(new VectorValue[]
            {
                new NumberValue(42),
                new NumberValue(43)
            }));
    }

    [Fact]
    public void SourceModuleCanUseStandardNativeDependencies()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule(
            "feature",
            "import lib.math; import lib.vector; " +
            "function calculate() { return lib.math.sqrt(81) + lib.vector.dot([1, 2], [3, 4]); }");

        AssertBackendsMatch(
            program,
            "import feature; feature.calculate();",
            new NumberValue(20));
    }

    [Fact]
    public void SourceModuleCanUsePluginRegisteredNativeDependency()
    {
        using var program = new TemporaryProgramRoot(includePlugin: true);
        program.WriteModule(
            "feature",
            "import plugin.demo; function calculate(value) { return plugin.demo.double(value); }");

        AssertBackendsMatch(
            program,
            "import feature; feature.calculate(21);",
            new NumberValue(42));
    }

    [Fact]
    public void ModuleFunctionsCaptureAndMutatePersistentTopLevelState()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule(
            "counter",
            "let value = 0; function increase() { value = value + 1; return value; }");

        AssertBackendsMatch(
            program,
            "import counter; [counter.increase(), counter.increase(), counter.value];",
            new ListValue(new VectorValue[]
            {
                new NumberValue(1),
                new NumberValue(2),
                new NumberValue(2)
            }));
    }

    [Fact]
    public void EscapingClosureFromSourceModuleRetainsModuleAndDependencyEnvironment()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule("data.values", "let answer = 40;");
        program.WriteModule(
            "factory",
            "import data.values; " +
            "let offset = 1; " +
            "function make() { function inner() { offset = offset + 1; return data.values.answer + offset; } return inner; }");

        AssertBackendsMatch(
            program,
            "import factory; let reader = factory.make(); [reader(), reader()];",
            new ListValue(new VectorValue[]
            {
                new NumberValue(42),
                new NumberValue(43)
            }));
    }

    [Fact]
    public void SourceModuleTopLevelCanCallItsOwnCompiledFunction()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule(
            "selfuse",
            "function double(value) { return value * 2; } let answer = double(21);");

        AssertBackendsMatch(
            program,
            "import selfuse; selfuse.answer;",
            new NumberValue(42));
    }

    [Fact]
    public void RuntimeErrorInsideImportedFunctionKeepsModuleSourceAttribution()
    {
        using var interpreterProgram = new TemporaryProgramRoot();
        using var vmProgram = new TemporaryProgramRoot();
        const string moduleSource = "function fail() { return 1 / 0; }";
        const string mainSource = "import broken; broken.fail();";
        interpreterProgram.WriteModule("broken", moduleSource);
        vmProgram.WriteModule("broken", moduleSource);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            ExecuteInterpreter(
                mainSource,
                interpreterProgram.CreateInterpreterLoader(),
                interpreterProgram.Host,
                interpreterProgram.MainPath));

        var vmError = Assert.Throws<RuntimeError>(() =>
            ExecuteVm(
                mainSource,
                vmProgram.CreateVmLoader(),
                vmProgram.Host,
                vmProgram.MainPath));

        Assert.Equal(interpreterError.Code, vmError.Code);
        Assert.Equal(interpreterError.Message, vmError.Message);
        Assert.Equal(interpreterError.Span, vmError.Span);
        Assert.Equal(vmProgram.ModulePath("broken"), vmError.SourceName);
        Assert.Equal(moduleSource, vmError.SourceText);
    }

    [Fact]
    public void LoadRemainsParseOnlyAndVmImportInitializesExactlyOnce()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule("delayed", "print(\"ran\"); let answer = 42;");
        var loader = program.CreateVmLoader();

        var module = loader.Load(Id("delayed"));

        Assert.False(loader.IsInitialized(Id("delayed")));
        Assert.Empty(program.Output);
        Assert.Throws<RuntimeError>(() => module.Environment.Get("answer", Span()));

        loader.Import(Id("delayed"), program.Host);
        loader.Import(Id("delayed"), program.Host);

        Assert.True(loader.IsInitialized(Id("delayed")));
        Assert.Equal(new[] { "ran" }, program.Output);
        Assert.Equal(new NumberValue(42), module.Environment.Get("answer", Span()));
        Assert.Single(loader.InitializedModules);
    }

    [Fact]
    public void CircularSourceImportsRemainRejectedBeforeVmInitialization()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule("a", "import b; let value = 1;");
        program.WriteModule("b", "import a; let value = 2;");
        var loader = program.CreateVmLoader();

        var error = Assert.Throws<ModuleLoadException>(() =>
            loader.Import(Id("a"), program.Host));

        Assert.Equal(ModuleLoadErrorKind.CircularImport, error.Kind);
        Assert.Empty(program.Output);
        Assert.Empty(loader.InitializedModules);
    }

    [Fact]
    public void IndirectSourceDependencyIsNotVisibleToVmCaller()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule("hidden", "let secret = 9;");
        program.WriteModule(
            "public",
            "import hidden; function value() { return hidden.secret; }");
        var loader = program.CreateVmLoader();

        var error = Assert.Throws<RuntimeError>(() =>
            ExecuteVm(
                "import public; hidden.secret;",
                loader,
                program.Host,
                program.MainPath));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);

        var value = ExecuteVm(
            "import public; public.value();",
            loader,
            program.Host,
            program.MainPath);
        Assert.Equal(new NumberValue(9), value);
    }

    private static void AssertBackendsMatch(
        TemporaryProgramRoot program,
        string mainSource,
        VectorValue expected)
    {
        var interpreterOutput = new List<string>();
        var vmOutput = new List<string>();
        var interpreterHost = new VectorHost(interpreterOutput.Add);
        var vmHost = new VectorHost(vmOutput.Add);

        var interpreterResult = ExecuteInterpreter(
            mainSource,
            program.CreateInterpreterLoader(),
            interpreterHost,
            program.MainPath);
        var vmResult = ExecuteVm(
            mainSource,
            program.CreateVmLoader(),
            vmHost,
            program.MainPath);

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
        Assert.Equal(interpreterOutput, vmOutput);
    }

    private static VectorValue ExecuteInterpreter(
        string source,
        ModuleLoader loader,
        IVectorHost host,
        string sourceName)
    {
        var syntax = Parse(source);
        return new Interpreter(host: host, moduleLoader: loader)
            .Execute(syntax, sourceName, source);
    }

    private static VectorValue ExecuteVm(
        string source,
        ModuleLoader loader,
        IVectorHost host,
        string sourceName)
    {
        var syntax = Parse(source);
        var compilation = new BytecodeCompiler().Compile(syntax, sourceName, source);
        return new VectorVirtualMachine(host: host, moduleLoader: loader)
            .Execute(compilation.Program)
            .Result;
    }

    private static CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private static SourceSpan Span() =>
        new(new SourcePosition(0, 1, 1), new SourcePosition(0, 1, 1));

    private sealed class DemoPlugin : IVectorPlugin
    {
        public string Id => "tests.source-module-vm";

        public int ApiVersion => VectorPluginApi.CurrentVersion;

        public void Register(IVectorPluginContext context)
        {
            context.RegisterModule(new NativeModuleDefinition(
                SourceModuleVmTests.Id("plugin.demo"),
                module => module.Export(
                    "double",
                    new NativeFunction(
                        "double",
                        1,
                        (_, arguments) => NativeValueConverter.FromNumber(
                            NativeValueConverter.ToNumber(arguments[0], "value") * 2)))));
        }
    }

    private sealed class TemporaryProgramRoot : IDisposable
    {
        private readonly bool _includePlugin;

        public TemporaryProgramRoot(bool includePlugin = false)
        {
            _includePlugin = includePlugin;
            Root = Path.Combine(Path.GetTempPath(), $"VectorSourceModuleVm-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            Output = new List<string>();
            Host = new VectorHost(Output.Add);
        }

        public string Root { get; }

        public string MainPath => Path.Combine(Root, "main.vec");

        public List<string> Output { get; }

        public VectorHost Host { get; }

        public ModuleLoader CreateInterpreterLoader() =>
            new(new ModuleResolver(Root), CreateRegistry());

        public ModuleLoader CreateVmLoader() =>
            new(
                new ModuleResolver(Root),
                CreateRegistry(),
                new BytecodeSourceModuleExecutor());

        public string ModulePath(string qualifiedName) =>
            new ModuleResolver(Root).Resolve(Id(qualifiedName));

        public void WriteModule(string qualifiedName, string source)
        {
            var path = ModulePath(qualifiedName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private NativeModuleRegistry CreateRegistry()
        {
            var registry = StandardLibraryRegistry.CreateDefault();
            if (_includePlugin)
            {
                new VectorPluginManager(registry).Register(new DemoPlugin());
            }

            return registry;
        }
    }
}
