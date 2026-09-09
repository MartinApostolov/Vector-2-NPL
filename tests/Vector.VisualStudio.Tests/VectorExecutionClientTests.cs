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
