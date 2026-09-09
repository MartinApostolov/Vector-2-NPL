namespace Vector.LanguageServer.Tests;

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using Vector.LanguageServer.Protocol;
using Xunit;

public sealed class LanguageServerLifecycleTests
{
    [Fact]
    public async Task Initialize_ReturnsIdentityAndEditorCapabilities()
    {
        var server = new VectorLanguageServer();

        VectorInitializeResult result = await server.InitializeAsync(CreateInitializeParams(), CancellationToken.None);

        Assert.Equal(ServerLifecycleState.Initialized, server.State);
        Assert.Equal("Vector Language Server", result.ServerInfo.Name);
        Assert.NotNull(result.Capabilities.TextDocumentSync);
        CompletionOptions completion = Assert.IsType<CompletionOptions>(result.Capabilities.CompletionProvider);
        Assert.Contains(".", Assert.IsType<string[]>(completion.TriggerCharacters));
        Assert.Equal(true, result.Capabilities.HoverProvider);
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

    private static JToken CreateInitializeParams() => JObject.Parse("{}");
}
