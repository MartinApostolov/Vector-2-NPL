namespace Vector.LanguageServer.Tests;

using System.Text.Json;
using Vector.LanguageServer.Protocol;
using Xunit;

public sealed class LanguageServerLifecycleTests
{
    [Fact]
    public async Task Initialize_ReturnsIdentityAndEmptyCapabilities()
    {
        var server = new VectorLanguageServer();

        InitializeResult result = await server.InitializeAsync(CreateInitializeParams(), CancellationToken.None);

        Assert.Equal(ServerLifecycleState.Initialized, server.State);
        Assert.Equal("Vector Language Server", result.ServerInfo.Name);
        Assert.NotNull(result.Capabilities);
    }

    [Fact]
    public async Task Initialize_CannotRunTwice()
    {
        var server = new VectorLanguageServer();
        await server.InitializeAsync(CreateInitializeParams(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.InitializeAsync(CreateInitializeParams(), CancellationToken.None));
    }

    [Fact]
    public async Task ShutdownAndExit_CompletesSuccessfully()
    {
        var server = new VectorLanguageServer();
        await server.InitializeAsync(CreateInitializeParams(), CancellationToken.None);
        await server.InitializedAsync(new object(), CancellationToken.None);

        object? response = await server.ShutdownAsync(CancellationToken.None);
        server.Exit();

        Assert.Null(response);
        Assert.Equal(ServerLifecycleState.Exited, server.State);
        Assert.Equal(0, server.ExitCode);
        Assert.True(server.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public void ExitWithoutShutdown_UsesFailureExitCode()
    {
        var server = new VectorLanguageServer();

        server.Exit();

        Assert.Equal(ServerLifecycleState.Exited, server.State);
        Assert.Equal(1, server.ExitCode);
    }

    [Fact]
    public async Task ShutdownBeforeInitialize_IsRejected()
    {
        var server = new VectorLanguageServer();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.ShutdownAsync(CancellationToken.None));
    }

    [Fact]
    public void Disconnect_CompletesAndMarksFailure()
    {
        var server = new VectorLanguageServer();

        server.NotifyDisconnected();

        Assert.Equal(ServerLifecycleState.Disconnected, server.State);
        Assert.Equal(1, server.ExitCode);
        Assert.True(server.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Initialize_HonorsCancellation()
    {
        var server = new VectorLanguageServer();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => server.InitializeAsync(CreateInitializeParams(), cancellation.Token));
        Assert.Equal(ServerLifecycleState.Created, server.State);
    }

    private static InitializeParams CreateInitializeParams()
    {
        using JsonDocument document = JsonDocument.Parse("{}");
        return new InitializeParams(null, null, document.RootElement.Clone());
    }
}
