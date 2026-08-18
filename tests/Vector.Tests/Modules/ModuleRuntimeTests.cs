using Vector.Core.Diagnostics;
using Vector.Core.Execution;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Callable;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Modules;

public sealed class ModuleRuntimeTests
{
    [Fact]
    public void ImportExecutesModuleTopLevelCodeAndExposesVariableByFullPath()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.geometry", "let pi = 3.14;");

        var result = program.Execute("import lib.geometry; lib.geometry.pi;");

        Assert.Equal(new NumberValue(3.14), result);
        Assert.True(program.Loader.IsInitialized(Id("lib.geometry")));
    }

    [Fact]
    public void QualifiedModuleFunctionCanBeCalled()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.math", "function add(a, b) { return a + b; }");

        var result = program.Execute("import lib.math; lib.math.add(5, 7);");

        Assert.Equal(new NumberValue(12), result);
    }

    [Fact]
    public void ModuleTopLevelInitializationRunsOnlyOnceForRepeatedImports()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("once", "print(\"initialized\"); let value = 1;");

        program.Execute("import once; import once; once.value;");

        Assert.Equal(new[] { "initialized" }, program.Output);
        Assert.Single(program.Loader.InitializedModules);
    }

    [Fact]
    public void ModuleInitializationRunsOnlyOnceAcrossSeparateInterpretersUsingSameLoader()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("once", "print(\"initialized\"); let value = 1;");

        program.Execute("import once; once.value;");
        program.Execute("import once; once.value;");

        Assert.Equal(new[] { "initialized" }, program.Output);
    }

    [Fact]
    public void DependencyInitializesBeforeImportingModule()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("dependency", "print(\"dependency\");");
        program.WriteModule("feature", "import dependency; print(\"feature\");");

        program.Execute("import feature;");

        Assert.Equal(new[] { "dependency", "feature" }, program.Output);
    }

    [Fact]
    public void SharedDependencyInitializesOnlyOnceAcrossImportGraph()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("shared", "print(\"shared\"); let value = 3;");
        program.WriteModule("left", "import shared; let value = shared.value;");
        program.WriteModule("right", "import shared; let value = shared.value;");

        program.Execute("import left; import right; left.value + right.value;");

        Assert.Equal(new[] { "shared" }, program.Output);
        Assert.Equal(3, program.Loader.InitializedModules.Count);
    }

    [Fact]
    public void ModuleFunctionReadsItsOwnTopLevelBinding()
    {
        using var program = new TemporaryProgram();
        program.WriteModule(
            "settings",
            "let base = 10; function plusBase(value) { return base + value; }");

        var result = program.Execute("import settings; settings.plusBase(5);");

        Assert.Equal(new NumberValue(15), result);
    }

    [Fact]
    public void ModuleFunctionCanAssignItsOwnTopLevelBinding()
    {
        using var program = new TemporaryProgram();
        program.WriteModule(
            "counter",
            "let value = 0; function increase() { value = value + 1; }");

        var result = program.Execute(
            "import counter; counter.increase(); counter.increase(); counter.value;");

        Assert.Equal(new NumberValue(2), result);
    }

    [Fact]
    public void ModuleFunctionCanCallQualifiedDependency()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("math.tools", "function twice(value) { return value * 2; }");
        program.WriteModule(
            "feature",
            "import math.tools; function calculate(value) { return math.tools.twice(value) + 1; }");

        var result = program.Execute("import feature; feature.calculate(6);");

        Assert.Equal(new NumberValue(13), result);
    }

    [Fact]
    public void EscapingClosureFromModuleRetainsQualifiedDependencyAccess()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("data.values", "let answer = 42;");
        program.WriteModule(
            "factory",
            "import data.values; " +
            "function make() { function inner() { return data.values.answer; } return inner; }");

        var result = program.Execute("import factory; let reader = factory.make(); reader();");

        Assert.Equal(new NumberValue(42), result);
    }

    [Fact]
    public void ModuleTopLevelCodeCanUseQualifiedDependency()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("constants", "let base = 7;");
        program.WriteModule("computed", "import constants; let result = constants.base * 3;");

        var result = program.Execute("import computed; computed.result;");

        Assert.Equal(new NumberValue(21), result);
    }

    [Fact]
    public void UnqualifiedModuleMemberDoesNotLeakIntoCallerScope()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.geometry", "let pi = 3.14;");

        var error = Assert.Throws<RuntimeError>(() =>
            program.Execute("import lib.geometry; pi;"));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
    }

    [Fact]
    public void ShortenedModulePathIsNotAutomaticallyAvailable()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.geometry", "let pi = 3.14;");

        var error = Assert.Throws<RuntimeError>(() =>
            program.Execute("import lib.geometry; geometry.pi;"));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
        Assert.Contains("geometry.pi", error.Message);
    }

    [Fact]
    public void QualifiedModuleCannotBeAccessedWithoutExplicitImportEvenIfAlreadyLoaded()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.geometry", "let pi = 3.14;");
        program.Loader.Import(Id("lib.geometry"), program.Host);

        var error = Assert.Throws<RuntimeError>(() => program.Execute("lib.geometry.pi;"));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
    }

    [Fact]
    public void IndirectDependencyIsNotAutomaticallyVisibleToCaller()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("hidden", "let secret = 9;");
        program.WriteModule("public", "import hidden; function value() { return hidden.secret; }");

        var error = Assert.Throws<RuntimeError>(() =>
            program.Execute("import public; hidden.secret;"));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
        Assert.Equal(new NumberValue(9), program.Execute("import public; public.value();"));
    }

    [Fact]
    public void MissingQualifiedMemberUsesStructuredUndefinedVariableError()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.geometry", "let pi = 3.14;");

        var error = Assert.Throws<RuntimeError>(() =>
            program.Execute("import lib.geometry; lib.geometry.missing;"));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
        Assert.Contains("missing", error.Message);
        Assert.True(error.Span.Length > 0);
    }

    [Fact]
    public void FullQualifiedPathsKeepSameNamedModulesDistinct()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("game.geometry", "let kind = 1;");
        program.WriteModule("math.geometry", "let kind = 2;");

        var result = program.Execute(
            "import game.geometry; import math.geometry; " +
            "game.geometry.kind + math.geometry.kind;");

        Assert.Equal(new NumberValue(3), result);
    }

    [Fact]
    public void ModuleBuiltinCallsUseProgramHost()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("speaker", "function speak(value) { print(text(value)); }");

        program.Execute("import speaker; speaker.speak(12.5);");

        Assert.Equal(new[] { "12.5" }, program.Output);
    }

    [Fact]
    public void ExistingModuleLoaderConstructorsKeepInterpreterSourceExecutionByDefault()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("default.runtime", "function identity(value) { return value; }");

        var module = program.Loader.Import(Id("default.runtime"), program.Host);

        Assert.IsType<UserFunction>(module.Environment.Get("identity", Span()));
    }

    [Fact]
    public void SourceModuleExecutionStrategyCanBeInjectedWithoutChangingParseOnlyLoad()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("custom", "let value = 42;");
        var executor = new RecordingSourceModuleExecutor();
        var loader = new ModuleLoader(
            new ModuleResolver(program.Root),
            new NativeModuleRegistry(),
            executor);

        var loaded = loader.Load(Id("custom"));

        Assert.Equal(0, executor.CallCount);
        Assert.False(loader.IsInitialized(Id("custom")));

        var imported = loader.Import(Id("custom"), program.Host);

        Assert.Same(loaded, imported);
        Assert.Equal(1, executor.CallCount);
        Assert.Same(loaded, executor.LastModule);
        Assert.Same(loader, executor.LastLoader);
        Assert.Same(program.Host, executor.LastHost);
        Assert.True(loader.IsInitialized(Id("custom")));
    }

    [Fact]
    public void LoadStillOnlyParsesAndDoesNotInitializeUntilImport()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("delayed", "print(\"ran\"); let answer = 42;");

        var module = program.Loader.Load(Id("delayed"));

        Assert.False(program.Loader.IsInitialized(Id("delayed")));
        Assert.Empty(program.Output);
        Assert.Throws<RuntimeError>(() => module.Environment.Get("answer", Span()));

        program.Loader.Import(Id("delayed"), program.Host);

        Assert.True(program.Loader.IsInitialized(Id("delayed")));
        Assert.Equal(new[] { "ran" }, program.Output);
        Assert.Equal(new NumberValue(42), module.Environment.Get("answer", Span()));
    }

    [Fact]
    public void ImportReturnsCachedLoadedModuleInstance()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("data", "let value = 4;");

        var loaded = program.Loader.Load(Id("data"));
        var imported = program.Loader.Import(Id("data"), program.Host);
        var importedAgain = program.Loader.Import(Id("data"), program.Host);

        Assert.Same(loaded, imported);
        Assert.Same(imported, importedAgain);
        Assert.Single(program.Loader.InitializedModules);
    }

    [Fact]
    public void ModuleFunctionsRemainCallableAfterImportingInterpreterIsGone()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("math", "function square(value) { return value * value; }");

        program.Execute("import math;");
        var result = program.Execute("import math; math.square(9);");

        Assert.Equal(new NumberValue(81), result);
    }

    [Fact]
    public void ModuleCanExposeListAndCallerCanReadItsContents()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("data", "let values = [2, 4, 6];");

        var result = program.Execute("import data; data.values[1];");

        Assert.Equal(new NumberValue(4), result);
    }

    [Fact]
    public void CallerCanPassItsValuesIntoQualifiedModuleFunction()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("math", "function add(a, b) { return a + b; }");

        var result = program.Execute(
            "import math; let first = 8; let second = 11; math.add(first, second);");

        Assert.Equal(new NumberValue(19), result);
    }

    [Fact]
    public void ModuleInitializationCanCallItsOwnFunction()
    {
        using var program = new TemporaryProgram();
        program.WriteModule(
            "selfuse",
            "function double(value) { return value * 2; } let answer = double(21);");

        var result = program.Execute("import selfuse; selfuse.answer;");

        Assert.Equal(new NumberValue(42), result);
    }

    [Fact]
    public void ImportedModuleBindingDoesNotCollideWithCallerBindingOfSameMemberName()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("settings", "let value = 20;");

        var result = program.Execute(
            "import settings; let value = 5; [value, settings.value];");

        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(5), new NumberValue(20) }),
            result);
    }

    [Fact]
    public void ModuleQualifiedAccessWorksInsideCallerFunction()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("constants", "let answer = 42;");

        var result = program.Execute(
            "import constants; function read() { return constants.answer; } read();");

        Assert.Equal(new NumberValue(42), result);
    }

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private static SourceSpan Span() =>
        new(new SourcePosition(0, 1, 1), new SourcePosition(0, 1, 1));

    private static Vector.Core.Syntax.CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }

    private sealed class RecordingSourceModuleExecutor : ISourceModuleExecutor
    {
        public int CallCount { get; private set; }

        public LoadedModule? LastModule { get; private set; }

        public IVectorHost? LastHost { get; private set; }

        public ModuleLoader? LastLoader { get; private set; }

        public void Execute(LoadedModule module, IVectorHost host, ModuleLoader moduleLoader)
        {
            CallCount++;
            LastModule = module;
            LastHost = host;
            LastLoader = moduleLoader;
        }
    }

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorModuleRuntimeTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            Output = new List<string>();
            Host = new VectorHost(Output.Add);
            Loader = new ModuleLoader(new ModuleResolver(Root));
        }

        public string Root { get; }

        public List<string> Output { get; }

        public VectorHost Host { get; }

        public ModuleLoader Loader { get; }

        public VectorValue Execute(string source)
        {
            var interpreter = new Interpreter(host: Host, moduleLoader: Loader);
            return interpreter.Execute(Parse(source));
        }

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
