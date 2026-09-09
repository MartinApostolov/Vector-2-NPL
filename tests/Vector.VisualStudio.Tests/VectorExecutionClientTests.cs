namespace Vector.VisualStudio.Tests;

using Vector.ExecutionProtocol;
using Vector.VisualStudio.Execution;
using Xunit;

public sealed class VectorExecutionClientTests
{
    [Theory]
    [InlineData(VectorExecutionEngine.Interpreter)]
    [InlineData(VectorExecutionEngine.Vm)]
    public async Task ExecuteAsync_RunsUnsavedSourceThroughSelectedEngine(VectorExecutionEngine engine)
    {
        var client = CreateClient();

        VectorExecutionResponse response = await client.ExecuteAsync(new VectorExecutionRequest
        {
            Source = "print(\"unsaved\");\n42;",
            Engine = engine,
        });

        Assert.True(response.Success);
        Assert.Equal(engine, response.Engine);
        Assert.Equal(["unsaved"], response.Output);
        Assert.Equal("42", response.Result);
    }

    [Fact]
    public async Task ExecuteAsync_DisassemblesWithoutExecutingSource()
    {
        var client = CreateClient();

        VectorExecutionResponse response = await client.ExecuteAsync(new VectorExecutionRequest
        {
            Source = "print(\"must not run\");\n42;",
            SourcePath = @"C:\workspace with spaces\unsaved.vec",
            Engine = VectorExecutionEngine.Vm,
            Operation = VectorExecutionOperation.Disassemble,
        });

        Assert.True(response.Success);
        Assert.Empty(response.Output);
        Assert.Null(response.Result);
        Assert.Contains("unsaved.vec", response.Disassembly, StringComparison.Ordinal);
        Assert.Contains("Call", response.Disassembly, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsStructuredFailureWhenHostIsMissing()
    {
        var client = new VectorExecutionClient(
            Path.Combine(Path.GetTempPath(), $"missing-vector-host-{Guid.NewGuid():N}.exe"),
            TimeSpan.FromSeconds(1));

        VectorExecutionResponse response = await client.ExecuteAsync(new VectorExecutionRequest { Source = "42;" });

        Assert.False(response.Success);
        Assert.Equal("host_not_found", Assert.IsType<VectorHostFailure>(response.HostFailure).Code);
    }

    [Fact]
    public async Task ExecuteAsync_TerminatesHostAfterTimeout()
    {
        var client = CreateClient(TimeSpan.FromMilliseconds(500));

        VectorExecutionResponse response = await client.ExecuteAsync(new VectorExecutionRequest
        {
            Source = "while true {}",
        });

        Assert.False(response.Success);
        Assert.Equal("host_timeout", Assert.IsType<VectorHostFailure>(response.HostFailure).Code);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCallerCancellation()
    {
        var client = CreateClient(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ExecuteAsync(
            new VectorExecutionRequest { Source = "while true {}" },
            cancellation.Token));
    }

    [Theory]
    [InlineData("print(\"line endings\");\n42;")]
    [InlineData("print(\"line endings\");\r\n42;")]
    public async Task ExecuteAsync_SupportsLfAndCrlf(string source)
    {
        VectorExecutionResponse response = await CreateClient().ExecuteAsync(
            new VectorExecutionRequest { Source = source });

        Assert.True(response.Success);
        Assert.Equal(["line endings"], response.Output);
        Assert.Equal("42", response.Result);
    }

    [Fact]
    public async Task ExecuteAsync_SupportsHostAndSourcePathsWithSpacesAndUnicode()
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "Vector host üñîçødé " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            string sourceHost = Path.GetDirectoryName(FindExecutionHost())!;
            foreach (string sourceFile in Directory.EnumerateFiles(sourceHost))
            {
                File.Copy(sourceFile, Path.Combine(temporaryRoot, Path.GetFileName(sourceFile)));
            }

            var client = new VectorExecutionClient(
                Path.Combine(temporaryRoot, "Vector.ExecutionHost.exe"),
                TimeSpan.FromSeconds(10));
            VectorExecutionResponse response = await client.ExecuteAsync(new VectorExecutionRequest
            {
                Source = "print(\"Здравей, Vector\");\n42;",
                SourcePath = Path.Combine(temporaryRoot, "пример файл.vec"),
                ProgramRoot = temporaryRoot,
            });

            Assert.True(response.Success);
            Assert.Equal(["Здравей, Vector"], response.Output);
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReportsInvalidPluginAcrossTheProcessBoundary()
    {
        string missingPlugin = Path.Combine(
            Path.GetTempPath(),
            "missing Vector plugin " + Guid.NewGuid().ToString("N"),
            "plugin.dll");

        VectorExecutionResponse response = await CreateClient().ExecuteAsync(new VectorExecutionRequest
        {
            Source = "42;",
            PluginPaths = [missingPlugin],
        });

        Assert.False(response.Success);
        Assert.Equal("plugin_load_failed", Assert.IsType<VectorHostFailure>(response.HostFailure).Code);
    }

    private static VectorExecutionClient CreateClient(TimeSpan? timeout = null) => new(
        FindExecutionHost(),
        timeout ?? TimeSpan.FromSeconds(10));

    private static string FindExecutionHost()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "src",
                "Vector.ExecutionHost",
                "bin",
                "Release",
                "net8.0",
                "Vector.ExecutionHost.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("The Release Vector execution host was not found from the test output directory.");
    }
}
