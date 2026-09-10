namespace Vector.VisualStudio;

using System.Diagnostics;
using System.IO.Pipelines;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.LanguageServer;
using Microsoft.VisualStudio.RpcContracts.LanguageServerProvider;
using Nerdbank.Streams;
using Vector.VisualStudio.Diagnostics;
using Vector.VisualStudio.Output;
using Vector.VisualStudio.Packaging;
using Vector.VisualStudio.Settings;

#pragma warning disable VSEXTPREVIEW_LSP
[VisualStudioContribution]
internal sealed class VectorLanguageServerProvider : LanguageServerProvider
{
    private const string LiveDiagnosticsEnvironmentVariable = "VECTOR_LIVE_DIAGNOSTICS";
    private const string ProgramRootEnvironmentVariable = "VECTOR_PROGRAM_ROOT";
    private readonly object processLock = new();
    private readonly VectorSettingsService settings;
    private readonly VectorLanguageServerLog log;
    private readonly VisualStudioExtensibility extensibility;
    private readonly VectorOutputService output;
    private readonly VectorExtensionPaths extensionPaths;
    private Process? process;
    private VectorIdeSettings? activeSettings;
    private bool initializationInProgress;
    private bool restartAfterInitialization;
    private int processGeneration;

    public VectorLanguageServerProvider(
        VectorSettingsService settings,
        TraceSource traceSource,
        VisualStudioExtensibility extensibility,
        VectorOutputService output)
    {
        this.settings = settings;
        this.log = new VectorLanguageServerLog(traceSource);
        this.extensibility = extensibility;
        this.output = output;
        this.extensionPaths = new VectorExtensionPaths();
        this.settings.Changed += this.OnSettingsChangedAsync;
        this.log.Information(
            $"Provider constructed. Assembly='{this.extensionPaths.ExtensionAssemblyPath}', " +
            $"extensionDirectory='{this.extensionPaths.ExtensionDirectory}', " +
            $"serviceHubBaseDirectory='{AppContext.BaseDirectory}', lifecycleLog='{this.log.FilePath}'.");
    }

    public override LanguageServerProviderConfiguration LanguageServerProviderConfiguration => new(
        "%Vector.VisualStudio.LanguageServer.DisplayName%",
        [DocumentFilter.FromDocumentType(VectorExtension.VectorDocumentType)]);

    public override async Task<IDuplexPipe?> CreateServerConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.StopServer("CreateServerConnectionAsync requested a replacement connection.");
        VectorIdeSettings currentSettings = await this.settings.GetAsync(cancellationToken);

        string executablePath = this.extensionPaths.LanguageServerPath;
        this.log.Information(
            $"CreateServerConnectionAsync resolving server. ExtensionDirectory='{this.extensionPaths.ExtensionDirectory}', " +
            $"ServiceHubBaseDirectory='{AppContext.BaseDirectory}', " +
            $"executable='{executablePath}', exists={File.Exists(executablePath)}, " +
            $"liveDiagnostics={currentSettings.LiveDiagnostics}, programRoot='{currentSettings.ProgramRoot ?? "<automatic>"}'.");
        if (!File.Exists(executablePath))
        {
            this.log.Error($"Vector language server executable was not found at '{executablePath}'.");
            await this.ReportFailureAsync(
                $"Vector language server executable was not found at '{executablePath}'.",
                cancellationToken);
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

        string runtimeEnvironment = DotNetChildProcessEnvironment.UseMachineWideRuntime(startInfo);
        this.log.Information($"Language server runtime selection: {runtimeEnvironment}.");

        int generation = Interlocked.Increment(ref this.processGeneration);
        var newProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        newProcess.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                this.log.Error($"Language server generation {generation} stderr: {args.Data}");
            }
        };
        newProcess.Exited += (_, _) => this.LogProcessExit(newProcess, generation);

        try
        {
            if (!newProcess.Start())
            {
                this.log.Error($"Process.Start returned false for '{executablePath}'.");
                await this.ReportFailureAsync(
                    $"Vector language server could not be started from '{executablePath}'.",
                    cancellationToken);
                newProcess.Dispose();
                return null;
            }

            lock (this.processLock)
            {
                this.process = newProcess;
                this.activeSettings = currentSettings;
                this.initializationInProgress = true;
                this.restartAfterInitialization = false;
            }

            newProcess.BeginErrorReadLine();
            this.log.Information(
                $"Language server generation {generation} started with PID {newProcess.Id}; waiting for the initialize result.");
            return new DuplexPipe(
                PipeReader.Create(newProcess.StandardOutput.BaseStream),
                PipeWriter.Create(newProcess.StandardInput.BaseStream));
        }
        catch (Exception exception)
        {
            lock (this.processLock)
            {
                if (ReferenceEquals(this.process, newProcess))
                {
                    this.process = null;
                    this.activeSettings = null;
                    this.initializationInProgress = false;
                }
            }

            this.log.Error(
                $"Language server generation {generation} failed while starting '{executablePath}': {exception}");
            await this.ReportFailureAsync(
                $"Vector language server failed to start: {exception.Message}",
                cancellationToken);
            newProcess.Dispose();
            return null;
        }
    }

    public override async Task OnServerInitializationResultAsync(
        ServerInitializationResult serverInitializationResult,
        LanguageServerInitializationFailureInfo? initializationFailureInfo,
        CancellationToken cancellationToken)
    {
        bool deferredRestart;
        lock (this.processLock)
        {
            this.initializationInProgress = false;
            deferredRestart = this.restartAfterInitialization;
            this.restartAfterInitialization = false;
        }

        if (serverInitializationResult == ServerInitializationResult.Failed)
        {
            string failureDetails = initializationFailureInfo?.ToString()
                ?? "Visual Studio provided no failure details.";
            this.log.Error(
                "Language server initialization failed. " +
                failureDetails);
            this.StopServer("Visual Studio reported a failed initialize request.");
            await this.ReportFailureAsync(
                $"Vector language server initialization failed. {failureDetails}",
                cancellationToken);
        }
        else
        {
            this.log.Information($"Language server initialization completed with result '{serverInitializationResult}'.");
            if (deferredRestart)
            {
                this.log.Information("Applying the analysis-settings restart deferred during initialization.");
                this.StopServer("Analysis settings changed while the initialize request was in flight.");
            }
        }

        await base.OnServerInitializationResultAsync(serverInitializationResult, initializationFailureInfo, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.settings.Changed -= this.OnSettingsChangedAsync;
            this.StopServer("The language-server provider is being disposed.");
        }

        base.Dispose(disposing);
    }

    private Task OnSettingsChangedAsync(VectorIdeSettings changedSettings)
    {
        VectorIdeSettings? runningSettings;
        bool initializing;
        lock (this.processLock)
        {
            runningSettings = this.activeSettings;
            initializing = this.initializationInProgress;
        }

        bool affectsAnalysis = runningSettings is not null
            && (runningSettings.LiveDiagnostics != changedSettings.LiveDiagnostics
                || !StringComparer.OrdinalIgnoreCase.Equals(runningSettings.ProgramRoot, changedSettings.ProgramRoot));
        this.log.Information(
            $"Settings notification received. serverRunning={runningSettings is not null}, " +
            $"initializationInProgress={initializing}, analysisSettingsChanged={affectsAnalysis}, " +
            $"liveDiagnostics={changedSettings.LiveDiagnostics}, programRoot='{changedSettings.ProgramRoot ?? "<automatic>"}'.");

        if (affectsAnalysis && initializing)
        {
            lock (this.processLock)
            {
                if (this.initializationInProgress)
                {
                    this.restartAfterInitialization = true;
                    this.log.Information("Settings restart deferred; StopServer() was not called during initialization.");
                    return Task.CompletedTask;
                }
            }
        }

        if (affectsAnalysis)
        {
            this.StopServer("Analysis settings changed after initialization.");
        }

        return Task.CompletedTask;
    }

    private void StopServer(string reason)
    {
        Process? oldProcess;
        bool stoppedDuringInitialization;
        lock (this.processLock)
        {
            oldProcess = this.process;
            stoppedDuringInitialization = this.initializationInProgress;
            this.process = null;
            this.activeSettings = null;
            this.initializationInProgress = false;
            this.restartAfterInitialization = false;
        }

        if (oldProcess is null)
        {
            this.log.Information($"StopServer() had no process to stop. Reason='{reason}'.");
            return;
        }

        int processId = TryGetProcessId(oldProcess);
        this.log.Information(
            $"StopServer() called for PID {processId}. duringInitialization={stoppedDuringInitialization}. Reason='{reason}'.");
        try
        {
            if (!oldProcess.HasExited)
            {
                oldProcess.StandardInput.Close();
                if (!oldProcess.WaitForExit(1000))
                {
                    this.log.Error($"Language server PID {processId} did not exit after stdin closed; killing its process tree.");
                    oldProcess.Kill(entireProcessTree: true);
                    oldProcess.WaitForExit(2000);
                }
            }

            if (oldProcess.HasExited)
            {
                oldProcess.WaitForExit();
                this.log.Information($"Language server PID {processId} stopped with exit code {oldProcess.ExitCode}.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            this.log.Error($"Stopping language server PID {processId} failed: {exception}");
        }
        finally
        {
            oldProcess.Dispose();
        }
    }

    private void LogProcessExit(Process exitedProcess, int generation)
    {
        int processId = TryGetProcessId(exitedProcess);
        bool isCurrentProcess;
        bool exitedDuringInitialization;
        lock (this.processLock)
        {
            isCurrentProcess = ReferenceEquals(this.process, exitedProcess);
            exitedDuringInitialization = isCurrentProcess && this.initializationInProgress;
        }

        try
        {
            string message =
                $"Language server generation {generation}, PID {processId}, exited with code {exitedProcess.ExitCode}. " +
                $"stopRequested={!isCurrentProcess}, beforeInitializeCompleted={exitedDuringInitialization}.";
            if (isCurrentProcess)
            {
                this.log.Error(message);
            }
            else
            {
                this.log.Information(message);
            }
        }
        catch (InvalidOperationException exception)
        {
            this.log.Error(
                $"Language server generation {generation}, PID {processId}, exited, but its exit code was unavailable: {exception.Message}");
        }
    }

    private static int TryGetProcessId(Process targetProcess)
    {
        try
        {
            return targetProcess.Id;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    private async Task ReportFailureAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            await this.output.WriteAsync(
                this.extensibility,
                $"{message} Lifecycle diagnostics: {this.log.FilePath}",
                cancellationToken);
        }
        catch (Exception exception)
        {
            this.log.Error($"Writing the language-server failure to the Vector Output channel failed: {exception}");
        }
    }
}
#pragma warning restore VSEXTPREVIEW_LSP
