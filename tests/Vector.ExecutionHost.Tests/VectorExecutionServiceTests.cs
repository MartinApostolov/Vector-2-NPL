namespace Vector.ExecutionHost.Tests;

using Vector.ExecutionProtocol;
using Xunit;

public sealed class VectorExecutionServiceTests
{
    [Theory]
    [InlineData(VectorExecutionEngine.Interpreter)]
    [InlineData(VectorExecutionEngine.Vm)]
    public void Run_CapturesOutputAndFormatsResultForBothEngines(VectorExecutionEngine engine)
    {
        var request = Request("print(\"hello\");\n[1, \"two\", true];", engine);

        VectorExecutionResponse response = new VectorExecutionService().Execute(request);

        Assert.True(response.Success);
        Assert.Equal(["hello"], response.Output);
        Assert.Equal("[1, \"two\", true]", response.Result);
        Assert.Empty(response.Diagnostics);
        Assert.Null(response.HostFailure);
    }

    [Fact]
    public void InterpreterAndVm_ProduceEquivalentProtocolResults()
    {
        const string source = "let values = range(1, 4); print(length(values)); values;";
        var service = new VectorExecutionService();

        VectorExecutionResponse interpreter = service.Execute(Request(source, VectorExecutionEngine.Interpreter));
        VectorExecutionResponse vm = service.Execute(Request(source, VectorExecutionEngine.Vm));

        Assert.True(interpreter.Success);
        Assert.True(vm.Success);
        Assert.Equal(interpreter.Output, vm.Output);
        Assert.Equal(interpreter.Result, vm.Result);
    }

    [Fact]
    public void Disassemble_CompilesWithoutExecutingProgramSideEffects()
    {
        var request = new VectorExecutionRequest
        {
            Source = "print(\"must not run\");\n42;",
            SourcePath = "unsaved.vec",
            Operation = VectorExecutionOperation.Disassemble,
            Engine = VectorExecutionEngine.Vm,
        };

        VectorExecutionResponse response = new VectorExecutionService().Execute(request);

        Assert.True(response.Success);
        Assert.Empty(response.Output);
        Assert.Null(response.Result);
        Assert.Contains("Call", response.Disassembly, StringComparison.Ordinal);
        Assert.Contains("unsaved.vec", response.Disassembly, StringComparison.Ordinal);
    }

    [Fact]
    public void SyntaxAndRuntimeFailures_ReturnStructuredDiagnosticsWithSourceIdentity()
    {
        var service = new VectorExecutionService();
        VectorExecutionResponse syntax = service.Execute(Request("let value = ;", sourcePath: "C:/work/syntax.vec"));
        VectorExecutionResponse runtime = service.Execute(Request("missing;", sourcePath: "C:/work/runtime.vec"));

        Assert.False(syntax.Success);
        Assert.Null(syntax.HostFailure);
        Assert.NotEmpty(syntax.Diagnostics);
        Assert.All(syntax.Diagnostics, diagnostic => Assert.Equal("C:/work/syntax.vec", diagnostic.SourceName));
        Assert.False(runtime.Success);
        VectorProtocolDiagnostic diagnostic = Assert.Single(runtime.Diagnostics);
        Assert.Equal("UndefinedVariable", diagnostic.Code);
        Assert.Equal("C:/work/runtime.vec", diagnostic.SourceName);
        Assert.Equal(0, diagnostic.Range.Start.Line);
    }

    [Theory]
    [InlineData(VectorExecutionEngine.Interpreter)]
    [InlineData(VectorExecutionEngine.Vm)]
    public void UnsavedSource_UsesExplicitProgramRootForLocalModules(VectorExecutionEngine engine)
    {
        string root = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "local"));
            File.WriteAllText(Path.Combine(root, "local", "values.vec"), "let answer = 42;");
            string sourcePath = Path.Combine(root, "main.vec");
            File.WriteAllText(sourcePath, "0;");
            var request = new VectorExecutionRequest
            {
                Source = "import local.values;\nprint(local.values.answer);\nlocal.values.answer;",
                SourcePath = sourcePath,
                ProgramRoot = root,
                Engine = engine,
            };

            VectorExecutionResponse response = new VectorExecutionService().Execute(request);

            Assert.True(response.Success);
            Assert.Equal(["42"], response.Output);
            Assert.Equal("42", response.Result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExplicitPluginPath_IsLoadedOnlyForRunRequest()
    {
        string pluginPath = FindBuildOutput(
            "tests",
            "Vector.TestPlugin.Acceptance",
            "Vector.TestPlugin.Acceptance.dll");
        var request = new VectorExecutionRequest
        {
            Source = "import accept.math;\naccept.math.double(21);",
            PluginPaths = [pluginPath],
            Engine = VectorExecutionEngine.Interpreter,
        };

        VectorExecutionResponse response = new VectorExecutionService().Execute(request);

        Assert.True(response.Success);
        Assert.Equal("42", response.Result);

        request = new VectorExecutionRequest
        {
            Source = "print(\"not executed\");",
            PluginPaths = [pluginPath],
            Operation = VectorExecutionOperation.Disassemble,
        };
        response = new VectorExecutionService().Execute(request);
        Assert.False(response.Success);
        Assert.Equal("plugins_not_allowed", response.HostFailure?.Code);
        Assert.Empty(response.Output);
        Assert.Null(response.Disassembly);
    }

    [Fact]
    public void InvalidPlugin_ReturnsHostFailureInsteadOfEscapingTheBoundary()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.dll");
        var request = new VectorExecutionRequest
        {
            Source = "1;",
            PluginPaths = [missing],
        };

        VectorExecutionResponse response = new VectorExecutionService().Execute(request);

        Assert.False(response.Success);
        Assert.Equal("plugin_load_failed", response.HostFailure?.Code);
        Assert.Empty(response.Diagnostics);
    }

    [Fact]
    public void NearbyPlugin_IsNeverAutoDiscovered()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string pluginPath = FindBuildOutput(
                "tests",
                "Vector.TestPlugin.Acceptance",
                "Vector.TestPlugin.Acceptance.dll");
            File.Copy(pluginPath, Path.Combine(root, Path.GetFileName(pluginPath)));
            var request = new VectorExecutionRequest
            {
                Source = "import accept.math;\naccept.math.answer;",
                ProgramRoot = root,
            };

            VectorExecutionResponse response = new VectorExecutionService().Execute(request);

            Assert.False(response.Success);
            Assert.Null(response.HostFailure);
            Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Code == "ModuleNotFound");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingProgramRoot_ReturnsStructuredFailureWithoutExecuting()
    {
        string missingRoot = Path.Combine(Path.GetTempPath(), "missing-vector-execution-" + Guid.NewGuid().ToString("N"));
        var request = new VectorExecutionRequest
        {
            Source = "print(\"must not run\");",
            ProgramRoot = missingRoot,
        };

        VectorExecutionResponse response = new VectorExecutionService().Execute(request);

        Assert.False(response.Success);
        Assert.Empty(response.Output);
        Assert.Equal("invalid_request", Assert.IsType<VectorHostFailure>(response.HostFailure).Code);
        Assert.Contains("does not exist", response.HostFailure.Message, StringComparison.Ordinal);
    }

    private static VectorExecutionRequest Request(
        string source,
        VectorExecutionEngine engine = VectorExecutionEngine.Interpreter,
        string? sourcePath = null) => new()
    {
        Source = source,
        SourcePath = sourcePath,
        Engine = engine,
    };

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "vector-execution-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindBuildOutput(params string[] path)
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))
            .Parent?.Name ?? "Release";
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vector.sln")))
            {
                string result = Path.Combine(
                    directory.FullName,
                    Path.Combine(path[..^1]),
                    "bin",
                    configuration,
                    "net8.0",
                    path[^1]);
                Assert.True(File.Exists(result), $"Expected build output at '{result}'.");
                return result;
            }
        }

        throw new InvalidOperationException("Could not locate Vector.sln from the test output directory.");
    }
}
