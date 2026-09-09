namespace Vector.ExecutionHost.Tests;

using System.Diagnostics;
using System.Text.Json;
using Vector.ExecutionProtocol;
using Xunit;

public sealed class ExecutionHostProtocolTests
{
    [Fact]
    public async Task Process_AcceptsOneJsonRequestAndReturnsOneStructuredResponse()
    {
        using Process process = StartHost();
        var request = new VectorExecutionRequest
        {
            Source = "print(\"protocol\");\n42;",
            SourcePath = "C:/workspace/unsaved.vec",
            Engine = VectorExecutionEngine.Vm,
        };

        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request, ExecutionProtocolJson.Options));
        process.StandardInput.Close();
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        VectorExecutionResponse response = JsonSerializer.Deserialize<VectorExecutionResponse>(
            output,
            ExecutionProtocolJson.Options)!;
        Assert.Equal(0, process.ExitCode);
        Assert.True(response.Success);
        Assert.Equal(["protocol"], response.Output);
        Assert.Equal("42", response.Result);
        Assert.True(string.IsNullOrEmpty(error));
    }

    [Fact]
    public async Task Process_MalformedJsonReturnsProtocolFailureAndNonzeroExit()
    {
        using Process process = StartHost();

        await process.StandardInput.WriteAsync("{ definitely not json");
        process.StandardInput.Close();
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        VectorExecutionResponse response = JsonSerializer.Deserialize<VectorExecutionResponse>(
            output,
            ExecutionProtocolJson.Options)!;
        Assert.Equal(2, process.ExitCode);
        Assert.False(response.Success);
        Assert.Equal("invalid_protocol", response.HostFailure?.Code);
    }

    private static Process StartHost()
    {
        string executable = Path.Combine(AppContext.BaseDirectory, "Vector.ExecutionHost.exe");
        Assert.True(File.Exists(executable), $"Execution host executable not found at '{executable}'.");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        Assert.True(process.Start());
        return process;
    }
}
