namespace Vector.LanguageServer;

using StreamJsonRpc;

internal static class Program
{
    public static async Task<int> Main()
    {
        await using Stream input = Console.OpenStandardInput();
        await using Stream output = Console.OpenStandardOutput();

        var formatter = new JsonMessageFormatter();
        using var handler = new HeaderDelimitedMessageHandler(output, input, formatter);
        using var rpc = new JsonRpc(handler);
        var server = new VectorLanguageServer(
            new JsonRpcLanguageClient(rpc),
            VectorLanguageServerOptions.FromEnvironment());

        rpc.AddLocalRpcTarget(server);
        rpc.Disconnected += (_, _) => server.NotifyDisconnected();
        rpc.StartListening();

        await Task.WhenAny(server.Completion, rpc.Completion).ConfigureAwait(false);
        return server.ExitCode;
    }
}
