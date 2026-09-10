namespace Vector.VisualStudio.Execution;

using System.Diagnostics;
using System.Text.Json;
using Vector.ExecutionProtocol;
using Vector.VisualStudio.Diagnostics;
using Vector.VisualStudio.Packaging;

public sealed class VectorExecutionClient
{
    private readonly string hostPath;
    private readonly TimeSpan timeout;
    private int activeHostProcessCount;

    internal int ActiveHostProcessCount => Volatile.Read(ref this.activeHostProcessCount);

    public VectorExecutionClient()
        : this(
            new VectorExtensionPaths().ExecutionHostPath,
            TimeSpan.FromSeconds(30))
    {
    }

    public VectorExecutionClient(string hostPath, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(hostPath))
        {
            throw new ArgumentException("An execution-host path is required.", nameof(hostPath));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        this.hostPath = Path.GetFullPath(hostPath);
        this.timeout = timeout;
    }

    public async Task<VectorExecutionResponse> ExecuteAsync(
        VectorExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(this.hostPath))
        {
            return ClientFailure(request, "host_not_found", $"Vector execution host was not found at '{this.hostPath}'.");
        }

        var startInfo = new ProcessStartInfo(this.hostPath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        DotNetChildProcessEnvironment.UseMachineWideRuntime(startInfo);

        using var process = new Process
        {
            StartInfo = startInfo,
        };

        try
        {
            if (!process.Start())
            {
                return ClientFailure(request, "host_start_failed", "Vector execution host did not start.");
            }
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return ClientFailure(request, "host_start_failed", error.Message);
        }

        Interlocked.Increment(ref this.activeHostProcessCount);
        try
        {
            return await this.ExchangeAsync(process, request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                Kill(process);
            }
            finally
            {
                Interlocked.Decrement(ref this.activeHostProcessCount);
            }
        }
    }

    private async Task<VectorExecutionResponse> ExchangeAsync(
        Process process,
        VectorExecutionRequest request,
        CancellationToken cancellationToken)
    {
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            string payload = JsonSerializer.Serialize(request, ExecutionProtocolJson.Options);
            await process.StandardInput.WriteAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();

            using var timeoutCancellation = new CancellationTokenSource(this.timeout);
            using var combined = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);
            await process.WaitForExitAsync(combined.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            await IgnoreProcessReadsAsync(outputTask, errorTask).ConfigureAwait(false);
            return ClientFailure(request, "host_timeout", $"Vector execution exceeded {this.timeout.TotalSeconds:G} seconds.");
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            await IgnoreProcessReadsAsync(outputTask, errorTask).ConfigureAwait(false);
            throw;
        }
        catch (Exception error) when (error is IOException or InvalidOperationException)
        {
            Kill(process);
            await IgnoreProcessReadsAsync(outputTask, errorTask).ConfigureAwait(false);
            return ClientFailure(request, "host_protocol_failure", error.Message);
        }

        string output = await outputTask.ConfigureAwait(false);
        string standardError = await errorTask.ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(output))
        {
            string detail = string.IsNullOrWhiteSpace(standardError)
                ? $"Vector execution host exited with code {process.ExitCode} without a response."
                : standardError.Trim();
            return ClientFailure(request, "host_crashed", detail);
        }

        try
        {
            return JsonSerializer.Deserialize<VectorExecutionResponse>(output, ExecutionProtocolJson.Options)
                ?? ClientFailure(request, "host_protocol_failure", "Vector execution host returned a null response.");
        }
        catch (JsonException error)
        {
            return ClientFailure(request, "host_protocol_failure", error.Message);
        }
    }

    private static VectorExecutionResponse ClientFailure(
        VectorExecutionRequest request,
        string code,
        string message) => new()
    {
        Success = false,
        Engine = request.Engine,
        Operation = request.Operation,
        HostFailure = new VectorHostFailure(code, message),
    };

    private static void Kill(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }

    private static async Task IgnoreProcessReadsAsync(params Task<string>[] reads)
    {
        try
        {
            await Task.WhenAll(reads).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
