namespace Vector.VisualStudio;

using System.Diagnostics;
using System.IO.Pipelines;
using System.Reflection;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.LanguageServer;
using Microsoft.VisualStudio.RpcContracts.LanguageServerProvider;
using Nerdbank.Streams;

#pragma warning disable VSEXTPREVIEW_LSP
[VisualStudioContribution]
internal sealed class VectorLanguageServerProvider : LanguageServerProvider
{
    private readonly object processLock = new();
    private Process? process;

    public override LanguageServerProviderConfiguration LanguageServerProviderConfiguration => new(
        "%Vector.VisualStudio.LanguageServer.DisplayName%",
        [DocumentFilter.FromDocumentType(VectorExtension.VectorDocumentType)]);

    public override Task<IDuplexPipe?> CreateServerConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.StopServer();

        string extensionDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("The Vector extension installation directory is unavailable.");
        string executablePath = Path.Combine(extensionDirectory, "LanguageServer", "Vector.LanguageServer.exe");
        if (!File.Exists(executablePath))
        {
            Trace.TraceError("Vector language server executable was not found in the extension payload.");
            return Task.FromResult<IDuplexPipe?>(null);
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var newProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        newProcess.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                Trace.TraceError("Vector language server: {0}", args.Data);
            }
        };

        try
        {
            if (!newProcess.Start())
            {
                newProcess.Dispose();
                return Task.FromResult<IDuplexPipe?>(null);
            }

            newProcess.BeginErrorReadLine();
            lock (this.processLock)
            {
                this.process = newProcess;
            }

            Trace.TraceInformation("Vector language server started (PID {0}).", newProcess.Id);
            return Task.FromResult<IDuplexPipe?>(new DuplexPipe(
                PipeReader.Create(newProcess.StandardOutput.BaseStream),
                PipeWriter.Create(newProcess.StandardInput.BaseStream)));
        }
        catch (Exception exception)
        {
            Trace.TraceError("Vector language server failed to start: {0}", exception.Message);
            newProcess.Dispose();
            return Task.FromResult<IDuplexPipe?>(null);
        }
    }

    public override Task OnServerInitializationResultAsync(
        ServerInitializationResult serverInitializationResult,
        LanguageServerInitializationFailureInfo? initializationFailureInfo,
        CancellationToken cancellationToken)
    {
        if (serverInitializationResult == ServerInitializationResult.Failed)
        {
            Trace.TraceError(
                "Vector language server initialization failed: {0}",
                initializationFailureInfo?.ToString() ?? "No failure details were provided.");
            this.StopServer();
        }

        return base.OnServerInitializationResultAsync(serverInitializationResult, initializationFailureInfo, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.StopServer();
        }

        base.Dispose(disposing);
    }

    private void StopServer()
    {
        Process? oldProcess;
        lock (this.processLock)
        {
            oldProcess = this.process;
            this.process = null;
        }

        if (oldProcess is null)
        {
            return;
        }

        try
        {
            if (!oldProcess.HasExited)
            {
                oldProcess.StandardInput.Close();
                if (!oldProcess.WaitForExit(1000))
                {
                    oldProcess.Kill(entireProcessTree: true);
                }
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            oldProcess.Dispose();
            Trace.TraceInformation("Vector language server stopped.");
        }
    }
}
#pragma warning restore VSEXTPREVIEW_LSP
