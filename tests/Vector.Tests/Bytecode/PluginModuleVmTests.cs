using Vector.Core.Bytecode.Compiler;
using Vector.Core.Bytecode.Vm;
using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Vector.Core.StandardLibrary;
using Vector.Core.Syntax;
using Vector.Plugins;
using Xunit;

namespace Vector.Tests.Bytecode;

public sealed class PluginModuleVmTests
{
    [Fact]
    public void RegistryPopulatedByVectorPluginsWorksWithoutVmSpecificPluginApi()
    {
        const string source = """
            import plugin.demo;
            [plugin.demo.answer, plugin.demo.double(21)];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(42),
                new NumberValue(42)
            }));
    }

    [Fact]
    public void PluginModuleRemainsFullyQualifiedAndExplicitlyImported()
    {
        using var program = new TemporaryProgramRoot();
        var loader = program.CreatePluginLoader();
        loader.Load(Id("plugin.demo"));

        AssertEquivalentRuntimeFailure(
            "plugin.demo.answer;",
            loader,
            DiagnosticCode.UndefinedVariable);

        using var secondProgram = new TemporaryProgramRoot();
        AssertEquivalentRuntimeFailure(
            "import plugin.demo; demo.answer;",
            secondProgram.CreatePluginLoader(),
            DiagnosticCode.UndefinedVariable);

        using var thirdProgram = new TemporaryProgramRoot();
        AssertEquivalentRuntimeFailure(
            "import plugin.demo; answer;",
            thirdProgram.CreatePluginLoader(),
            DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void PluginFunctionFailureMatchesInterpreterAtQualifiedCallSite()
    {
        using var program = new TemporaryProgramRoot();
        AssertEquivalentRuntimeFailure(
            "import plugin.demo; plugin.demo.fail();",
            program.CreatePluginLoader(),
            DiagnosticCode.RuntimeTypeError);
    }

    [Fact]
    public void PluginAndStandardModulesShareOneRegistryOnVm()
    {
        const string source = """
            import lib.math;
            import plugin.demo;
            plugin.demo.double(lib.math.sqrt(441));
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(42));
    }

    [Fact]
    public void PluginModuleIdentityAndInitializationAreCached()
    {
        using var program = new TemporaryProgramRoot();
        var loader = program.CreatePluginLoader();
        const string source = "import plugin.demo; import plugin.demo; plugin.demo.answer;";
        var syntax = Parse(source);
        var compilation = new BytecodeCompiler().Compile(syntax, "plugin-modules-vm.vec", source);

        var result = new VectorVirtualMachine(moduleLoader: loader).Execute(compilation.Program).Result;

        Assert.Equal(new NumberValue(42), result);
        Assert.Single(loader.InitializedModules, module => module.Id.Equals(Id("plugin.demo")));
        Assert.Same(loader.Load(Id("plugin.demo")), loader.Load(Id("plugin.demo")));
    }

    [Fact]
    public void PluginModuleConflictWithLocalSourceIsPreservedOnVmImport()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule("plugin.demo", "let local = 1;");
        var loader = program.CreatePluginLoader();
        const string source = "import plugin.demo;";
        var syntax = Parse(source);
        var compilation = new BytecodeCompiler().Compile(syntax, "plugin-modules-vm.vec", source);

        var error = Assert.Throws<ModuleLoadException>(() =>
            new VectorVirtualMachine(moduleLoader: loader).Execute(compilation.Program));

        Assert.Equal(ModuleLoadErrorKind.ModuleConflict, error.Kind);
    }

    private static void AssertVmMatchesInterpreter(string source, VectorValue expected)
    {
        using var interpreterProgram = new TemporaryProgramRoot();
        using var vmProgram = new TemporaryProgramRoot();
        var syntax = Parse(source);

        var interpreterResult = new Interpreter(moduleLoader: interpreterProgram.CreatePluginLoader())
            .Execute(syntax, "plugin-modules-vm.vec", source);

        var compilation = new BytecodeCompiler().Compile(syntax, "plugin-modules-vm.vec", source);
        var vmResult = new VectorVirtualMachine(moduleLoader: vmProgram.CreatePluginLoader())
            .Execute(compilation.Program)
            .Result;

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static void AssertEquivalentRuntimeFailure(
        string source,
        ModuleLoader vmLoader,
        DiagnosticCode expectedCode)
    {
        using var interpreterProgram = new TemporaryProgramRoot();
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter(moduleLoader: interpreterProgram.CreatePluginLoader())
                .Execute(syntax, "plugin-modules-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "plugin-modules-vm.vec", source);
        var vmError = Assert.Throws<RuntimeError>(() =>
            new VectorVirtualMachine(moduleLoader: vmLoader)
                .Execute(compilation.Program));

        Assert.Equal(expectedCode, interpreterError.Code);
        Assert.Equal(interpreterError.Code, vmError.Code);
        Assert.Equal(interpreterError.Message, vmError.Message);
        Assert.Equal(interpreterError.Span, vmError.Span);
        Assert.Equal(interpreterError.SourceName, vmError.SourceName);
        Assert.Equal(interpreterError.SourceText, vmError.SourceText);
    }

    private static CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private sealed class DemoPlugin : IVectorPlugin
    {
        public string Id => "tests.vm.plugin";

        public int ApiVersion => VectorPluginApi.CurrentVersion;

        public void Register(IVectorPluginContext context)
        {
            context.RegisterModule(new NativeModuleDefinition(
                PluginModuleVmTests.Id("plugin.demo"),
                module =>
                {
                    module.Export("answer", new NumberValue(42));
                    module.Export(
                        "double",
                        new NativeFunction(
                            "double",
                            1,
                            (_, arguments) => NativeValueConverter.FromNumber(
                                NativeValueConverter.ToNumber(arguments[0], "value") * 2)));
                    module.Export(
                        "fail",
                        new NativeFunction(
                            "fail",
                            0,
                            (_, _) => throw new NativeRuntimeException(
                                DiagnosticCode.RuntimeTypeError,
                                "Plugin deliberate failure.")));
                }));
        }
    }

    private sealed class TemporaryProgramRoot : IDisposable
    {
        public TemporaryProgramRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorPluginModuleVm-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public ModuleLoader CreatePluginLoader()
        {
            var registry = StandardLibraryRegistry.CreateDefault();
            var plugins = new VectorPluginManager(registry);
            plugins.Register(new DemoPlugin());
            return new ModuleLoader(new ModuleResolver(Root), registry);
        }

        public void WriteModule(string qualifiedName, string source)
        {
            var segments = qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var path = Path.Combine(Root, Path.Combine(segments)) + ".vec";
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
    }
}
