namespace Vector.VisualStudio.Tests;

using Vector.ExecutionProtocol;
using Vector.VisualStudio.Execution;
using Vector.VisualStudio.Settings;
using Xunit;

public sealed class VectorIdeSettingsTests
{
    [Fact]
    public void Defaults_UseInterpreterDiagnosticsAndAutoRoot()
    {
        VectorIdeSettings settings = VectorIdeSettings.Default;

        Assert.Equal(VectorExecutionEngine.Interpreter, settings.DefaultEngine);
        Assert.True(settings.LiveDiagnostics);
        Assert.Null(settings.ProgramRoot);
        Assert.Empty(settings.PluginPaths);
    }

    [Fact]
    public void FromRaw_ParsesVmRootAndDistinctPluginPaths()
    {
        VectorIdeSettings settings = VectorIdeSettings.FromRaw(
            "vm",
            liveDiagnostics: false,
            "  C:\\Vector Workspace  ",
            " first.dll;second.dll\r\nfirst.dll ");

        Assert.Equal(VectorExecutionEngine.Vm, settings.DefaultEngine);
        Assert.False(settings.LiveDiagnostics);
        Assert.Equal("C:\\Vector Workspace", settings.ProgramRoot);
        Assert.Equal(["first.dll", "second.dll"], settings.PluginPaths);
    }

    [Fact]
    public void RequestFactory_UsesDefaultAndExplicitEngines()
    {
        VectorIdeSettings settings = VectorIdeSettings.Default with
        {
            DefaultEngine = VectorExecutionEngine.Vm,
        };

        VectorExecutionRequest defaultRequest = Assert.IsType<VectorExecutionRequest>(
            VectorExecutionRequestFactory.Create("42;", null, settings).Request);
        VectorExecutionRequest explicitRequest = Assert.IsType<VectorExecutionRequest>(
            VectorExecutionRequestFactory.Create(
                "42;",
                null,
                settings,
                VectorExecutionEngine.Interpreter).Request);

        Assert.Equal(VectorExecutionEngine.Vm, defaultRequest.Engine);
        Assert.Equal(VectorExecutionEngine.Interpreter, explicitRequest.Engine);
    }

    [Fact]
    public void RequestFactory_AutoRootsPhysicalFiles()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), "vector project", "main.vec");

        VectorExecutionRequest request = Assert.IsType<VectorExecutionRequest>(
            VectorExecutionRequestFactory.Create("42;", sourcePath, VectorIdeSettings.Default).Request);

        Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(sourcePath)), request.ProgramRoot);
    }

    [Fact]
    public void RequestFactory_RejectsMissingExplicitRootBeforeExecution()
    {
        string missingRoot = Path.Combine(Path.GetTempPath(), "missing-vector-root-" + Guid.NewGuid().ToString("N"));
        VectorIdeSettings settings = VectorIdeSettings.Default with { ProgramRoot = missingRoot };

        VectorExecutionPreparation preparation = VectorExecutionRequestFactory.Create("42;", null, settings);

        Assert.Null(preparation.Request);
        Assert.Equal("invalid_program_root", Assert.IsType<VectorHostFailure>(preparation.Failure).Code);
    }

    [Fact]
    public void RequestFactory_PropagatesPluginsOnlyToExplicitRuns()
    {
        string plugin = Path.Combine(Path.GetTempPath(), "trusted plugin.dll");
        VectorIdeSettings settings = VectorIdeSettings.Default with { PluginPaths = [plugin] };

        VectorExecutionRequest run = Assert.IsType<VectorExecutionRequest>(
            VectorExecutionRequestFactory.Create("42;", null, settings).Request);
        VectorExecutionRequest disassemble = Assert.IsType<VectorExecutionRequest>(
            VectorExecutionRequestFactory.Create(
                "42;",
                null,
                settings,
                VectorExecutionEngine.Vm,
                VectorExecutionOperation.Disassemble).Request);

        Assert.Equal([Path.GetFullPath(plugin)], run.PluginPaths);
        Assert.Empty(disassemble.PluginPaths);
    }

    [Fact]
    public async Task ConfiguredPluginPath_RunsOnlyThroughTheIsolatedHost()
    {
        string plugin = FindBuildOutput(
            "tests",
            "Vector.TestPlugin.Acceptance",
            "Vector.TestPlugin.Acceptance.dll");
        VectorIdeSettings settings = VectorIdeSettings.Default with { PluginPaths = [plugin] };
        VectorExecutionRequest request = Assert.IsType<VectorExecutionRequest>(
            VectorExecutionRequestFactory.Create(
                "import accept.math;\naccept.math.double(21);",
                null,
                settings).Request);

        VectorExecutionResponse response = await new VectorExecutionClient(
            FindBuildOutput("src", "Vector.ExecutionHost", "Vector.ExecutionHost.exe"),
            TimeSpan.FromSeconds(10)).ExecuteAsync(request);

        Assert.True(response.Success);
        Assert.Equal("42", response.Result);
    }

    private static string FindBuildOutput(params string[] path)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "Vector.sln")))
            {
                continue;
            }

            string result = Path.Combine(
                directory.FullName,
                Path.Combine(path[..^1]),
                "bin",
                "Release",
                "net8.0",
                path[^1]);
            Assert.True(File.Exists(result), $"Expected Release build output at '{result}'.");
            return result;
        }

        throw new InvalidOperationException("Could not locate Vector.sln from the test output directory.");
    }
}
