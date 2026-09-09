namespace Vector.LanguageServer;

using System.Reflection;
using StreamJsonRpc;
using Vector.LanguageServer.Protocol;

public sealed class VectorLanguageServer
{
    private readonly object stateLock = new();
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ServerLifecycleState state;

    public ServerLifecycleState State
    {
        get
        {
            lock (this.stateLock)
            {
                return this.state;
            }
        }
    }

    public Task Completion => this.completion.Task;

    public int ExitCode { get; private set; }

    [JsonRpcMethod("initialize", UseSingleObjectParameterDeserialization = true)]
    public Task<InitializeResult> InitializeAsync(InitializeParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.stateLock)
        {
            if (this.state != ServerLifecycleState.Created)
            {
                throw new InvalidOperationException("The Vector language server can only be initialized once.");
            }

            this.state = ServerLifecycleState.Initialized;
        }

        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        return Task.FromResult(new InitializeResult(new ServerCapabilities(), new ServerInfo("Vector Language Server", version)));
    }

    [JsonRpcMethod("initialized", UseSingleObjectParameterDeserialization = true)]
    public Task InitializedAsync(object? request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.RequireState(ServerLifecycleState.Initialized, "initialized");
        return Task.CompletedTask;
    }

    [JsonRpcMethod("shutdown")]
    public Task<object?> ShutdownAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.stateLock)
        {
            if (this.state != ServerLifecycleState.Initialized)
            {
                throw new InvalidOperationException("The Vector language server must be initialized before shutdown.");
            }

            this.state = ServerLifecycleState.Shutdown;
        }

        return Task.FromResult<object?>(null);
    }

    [JsonRpcMethod("exit")]
    public void Exit()
    {
        lock (this.stateLock)
        {
            this.ExitCode = this.state == ServerLifecycleState.Shutdown ? 0 : 1;
            this.state = ServerLifecycleState.Exited;
        }

        this.completion.TrySetResult();
    }

    public void NotifyDisconnected()
    {
        lock (this.stateLock)
        {
            if (this.state == ServerLifecycleState.Exited)
            {
                return;
            }

            this.ExitCode = this.state == ServerLifecycleState.Shutdown ? 0 : 1;
            this.state = ServerLifecycleState.Disconnected;
        }

        this.completion.TrySetResult();
    }

    private void RequireState(ServerLifecycleState expected, string operation)
    {
        lock (this.stateLock)
        {
            if (this.state != expected)
            {
                throw new InvalidOperationException($"Cannot process '{operation}' while the language server is {this.state}.");
            }
        }
    }
}
