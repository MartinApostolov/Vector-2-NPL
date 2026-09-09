namespace Vector.ExecutionHost;

using System.Text.Json;
using Vector.ExecutionProtocol;

internal static class Program
{
    public static int Main()
    {
        TextWriter protocolOutput = Console.Out;
        Console.SetOut(TextWriter.Null);
        string payload = Console.In.ReadToEnd();

        VectorExecutionResponse response;
        int exitCode;
        try
        {
            VectorExecutionRequest request = JsonSerializer.Deserialize<VectorExecutionRequest>(
                payload,
                ExecutionProtocolJson.Options)
                ?? throw new JsonException("The execution request cannot be null.");
            response = new VectorExecutionService().Execute(request);
            exitCode = 0;
        }
        catch (JsonException error)
        {
            response = new VectorExecutionResponse
            {
                Success = false,
                HostFailure = new VectorHostFailure("invalid_protocol", error.Message),
            };
            exitCode = 2;
        }
        catch (Exception error)
        {
            response = new VectorExecutionResponse
            {
                Success = false,
                HostFailure = new VectorHostFailure(
                    "host_failure",
                    $"{error.GetType().Name}: {error.Message}"),
            };
            exitCode = 1;
        }

        protocolOutput.WriteLine(JsonSerializer.Serialize(response, ExecutionProtocolJson.Options));
        protocolOutput.Flush();
        return exitCode;
    }
}
