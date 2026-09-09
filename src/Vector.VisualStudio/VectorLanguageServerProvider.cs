namespace Vector.VisualStudio;

using System.Diagnostics;
using System.IO.Pipelines;
using System.Reflection;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.LanguageServer;
using Microsoft.VisualStudio.RpcContracts.LanguageServerProvider;
using Nerdbank.Streams;
using Vector.VisualStudio.Settings;

#pragma warning disable VSEXTPREVIEW_LSP
[VisualStudioContribution]
internal sealed class VectorLanguageServerProvider : LanguageServerProvider
{
    private const string LiveDiagnosticsEnvironmentVariable = "VECTOR_LIVE_DIAGNOSTICS";
    private const string ProgramRootEnvironmentVariable = "VECTOR_PROGRAM_ROOT";
    private readonly object processLock = new();
    private readonly VectorSettingsService settings;
    private Process? process;
    private VectorIdeSettings? activeSettings;

    public VectorLanguageServerProvider(VectorSettingsService settings)
    {
        this.settings = settings;
        this.settings.Changed += this.OnSettingsChangedAsync;
    }

    public override LanguageServerProviderConfiguration LanguageServerProviderConfiguration => new(
        "%Vector.VisualStudio.LanguageServer.DisplayName%",
        [DocumentFilter.FromDocumentType(VectorExtension.VectorDocumentType)]);

    public override async Task<IDuplexPipe?> CreateServerConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.StopServer();
        VectorIdeSettings currentSettings = await this.settings.GetAsync(cancellationToken);

        string extensionDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("The Vector extension installation directory is unavailable.");
        string executablePath = Path.Combine(extensionDirectory, "LanguageServer", "Vector.LanguageServer.exe");
        if (!File.Exists(executablePath))
        {
            Trace.TraceError("Vector language server executable was not found in the extension payload.");
            return null;
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
        startInfo.Environment[LiveDiagnosticsEnvironmentVariable] = currentSettings.LiveDiagnostics.ToString();
        if (currentSettings.ProgramRoot is not null)
        {
            startInfo.Environment[ProgramRootEnvironmentVariable] = currentSettings.ProgramRoot;
        }

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
                return null;
            }

            newProcess.BeginErrorReadLine();
            lock (this.processLock)
            {
                this.process = newProcess;
                this.activeSettings = currentSettings;
            }

            Trace.TraceInformation("Vector language server started (PID {0}).", newProcess.Id);
            return new DuplexPipe(
                PipeReader.Create(newProcess.StandardOutput.BaseStream),
                PipeWriter.Create(newProcess.StandardInput.BaseStream));
        }
        catch (Exception exception)
        {
            Trace.TraceError("Vector language server failed to start: {0}", exception.Message);
            newProcess.Dispose();
            return null;
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
            this.settings.Changed -= this.OnSettingsChangedAsync;
            this.StopServer();
        }

        base.Dispose(disposing);
    }

    private Task OnSettingsChangedAsync(VectorIdeSettings changedSettings)
    {
        VectorIdeSettings? runningSettings;
        lock (this.processLock)
        {
            runningSettings = this.activeSettings;
        }

        if (runningSettings is not null
            && (runningSettings.LiveDiagnostics != changedSettings.LiveDiagnostics
                || !StringComparer.OrdinalIgnoreCase.Equals(
                    runningSettings.ProgramRoot,
                    changedSettings.ProgramRoot)))
        {
            Trace.TraceInformation("Restarting the Vector language server because analysis settings changed.");
            this.StopServer();
        }

        return Task.CompletedTask;
    }

    private void StopServer()
    {
        Process? oldProcess;
        lock (this.processLock)
        {
            oldProcess = this.process;
            this.process = null;
            this.activeSettings = null;
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
