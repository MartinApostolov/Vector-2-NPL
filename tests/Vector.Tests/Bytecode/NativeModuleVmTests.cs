using Vector.Core.Bytecode;
using Vector.Core.Bytecode.Compiler;
using Vector.Core.Bytecode.Vm;
using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Vector.Core.StandardLibrary;
using Vector.Core.Syntax;
using Xunit;

namespace Vector.Tests.Bytecode;

public sealed class NativeModuleVmTests
{
    [Fact]
    public void CompilerEmitsImportAndQualifiedMemberInstructions()
    {
        const string source = "import lib.math; lib.math.sqrt(81);";
        var compilation = Compile(source);
        var chunk = compilation.Program.EntryPoint;

        Assert.Equal(new[] { "lib.math" }, chunk.Modules.Select(module => module.QualifiedName));
        Assert.Contains("lib.math.sqrt", chunk.Names);
        Assert.Contains(chunk.Instructions, instruction => instruction.OpCode == OpCode.Import);
        Assert.Contains(chunk.Instructions, instruction => instruction.OpCode == OpCode.GetQualifiedMember);
    }

    [Fact]
    public void VmUsesAllStandardNativeModulesLikeInterpreter()
    {
        const string source = """
            import lib.math;
            import lib.collections;
            import lib.io;
            import lib.vector;
            import lib.matrix;

            [
                lib.math.sqrt(81),
                lib.collections.sum([1, 2, 3]),
                lib.io.readLine(),
                lib.vector.dot([1, 2], [3, 4]),
                lib.matrix.multiply([[1, 2]], [[3], [4]])
            ];
            """;

        var expected = new ListValue(new VectorValue[]
        {
            new NumberValue(9),
            new NumberValue(6),
            new TextValue("Ada"),
            new NumberValue(11),
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[] { new NumberValue(11) })
            })
        });

        var interpreterInput = new Queue<string?>(new string?[] { "Ada" });
        var vmInput = new Queue<string?>(new string?[] { "Ada" });

        AssertVmMatchesInterpreter(
            source,
            expected,
            new VectorInputHost(null, () => interpreterInput.Dequeue()),
            new VectorInputHost(null, () => vmInput.Dequeue()));

        Assert.Empty(interpreterInput);
        Assert.Empty(vmInput);
    }

    [Fact]
    public void RepresentativeMathAndVectorProgramReturnsNineAndEleven()
    {
        const string source = """
            import lib.math;
            import lib.vector;

            [
                lib.math.sqrt(81),
                lib.vector.dot([1, 2], [3, 4])
            ];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(9),
                new NumberValue(11)
            }));
    }

    [Fact]
    public void NativeModuleIsInitializedOnceAndCachedAcrossRepeatedImports()
    {
        const string source = "import lib.math; import lib.math; lib.math.pi;";
        using var program = new TemporaryProgramRoot();
        var loader = program.CreateStandardLoader();

        var syntax = Parse(source);
        var compilation = new BytecodeCompiler().Compile(syntax, "native-modules-vm.vec", source);
        var result = new VectorVirtualMachine(moduleLoader: loader).Execute(compilation.Program).Result;

        Assert.IsType<NumberValue>(result);
        Assert.Single(loader.InitializedModules, module => module.Id.Equals(Id("lib.math")));

        var first = loader.Load(Id("lib.math"));
        var second = loader.Load(Id("lib.math"));
        Assert.Same(first, second);
    }

    [Fact]
    public void LoadedButUnimportedNativeModuleIsNotVisible()
    {
        const string source = "lib.math.sqrt(9);";
        using var program = new TemporaryProgramRoot();
        var loader = program.CreateStandardLoader();
        loader.Load(Id("lib.math"));

        AssertEquivalentRuntimeFailure(source, loader, DiagnosticCode.UndefinedVariable);
    }

    [Theory]
    [InlineData("import lib.math; math.sqrt(9);")]
    [InlineData("import lib.math; sqrt(9);")]
    [InlineData("import lib.math; lib.sqrt(9);")]
    public void ImportsDoNotCreateAliasesOrUnqualifiedLeakage(string source)
    {
        using var program = new TemporaryProgramRoot();
        AssertEquivalentRuntimeFailure(
            source,
            program.CreateStandardLoader(),
            DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void MissingQualifiedMemberMatchesInterpreterDiagnostic()
    {
        using var program = new TemporaryProgramRoot();
        AssertEquivalentRuntimeFailure(
            "import lib.math; lib.math.missing;",
            program.CreateStandardLoader(),
            DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void SourceAndStandardNativeModuleConflictIsPreservedOnVmImport()
    {
        using var program = new TemporaryProgramRoot();
        program.WriteModule("lib.math", "let replacement = 1;");
        var loader = program.CreateStandardLoader();
        const string source = "import lib.math;";
        var syntax = Parse(source);
        var compilation = new BytecodeCompiler().Compile(syntax, "native-modules-vm.vec", source);

        var error = Assert.Throws<ModuleLoadException>(() =>
            new VectorVirtualMachine(moduleLoader: loader).Execute(compilation.Program));

        Assert.Equal(ModuleLoadErrorKind.ModuleConflict, error.Kind);
        Assert.Contains("both a local Vector source file and a registered native module", error.Message);
    }

    private static void AssertVmMatchesInterpreter(
        string source,
        VectorValue expected,
        IVectorHost? interpreterHost = null,
        IVectorHost? vmHost = null)
    {
        using var interpreterProgram = new TemporaryProgramRoot();
        using var vmProgram = new TemporaryProgramRoot();
        var syntax = Parse(source);

        var interpreterResult = new Interpreter(
            host: interpreterHost,
            moduleLoader: interpreterProgram.CreateStandardLoader())
            .Execute(syntax, "native-modules-vm.vec", source);

        var compilation = new BytecodeCompiler().Compile(syntax, "native-modules-vm.vec", source);
        var vmResult = new VectorVirtualMachine(
            host: vmHost,
            moduleLoader: vmProgram.CreateStandardLoader())
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
            new Interpreter(moduleLoader: interpreterProgram.CreateStandardLoader())
                .Execute(syntax, "native-modules-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "native-modules-vm.vec", source);
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

    private static BytecodeCompilationResult Compile(string source)
    {
        var syntax = Parse(source);
        return new BytecodeCompiler().Compile(syntax, "native-modules-vm.vec", source);
    }

    private static CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private sealed class TemporaryProgramRoot : IDisposable
    {
        public TemporaryProgramRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorNativeModuleVm-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public ModuleLoader CreateStandardLoader() =>
            new(new ModuleResolver(Root), StandardLibraryRegistry.CreateDefault());

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
