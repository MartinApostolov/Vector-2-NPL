namespace Vector.ExecutionHost.Protocol;

using System.Text.Json;
using System.Text.Json.Serialization;

public enum VectorExecutionEngine
{
    Interpreter,
    Vm,
}

public enum VectorExecutionOperation
{
    Run,
    Disassemble,
}

public sealed class VectorExecutionRequest
{
    public required string Source { get; init; }

    public string? SourcePath { get; init; }

    public string? ProgramRoot { get; init; }

    public VectorExecutionEngine Engine { get; init; } = VectorExecutionEngine.Interpreter;

    public VectorExecutionOperation Operation { get; init; } = VectorExecutionOperation.Run;

    public string[] PluginPaths { get; init; } = [];
}

public sealed record VectorProtocolPosition(int Offset, int Line, int Character);

public sealed record VectorProtocolRange(VectorProtocolPosition Start, VectorProtocolPosition End);

public sealed record VectorProtocolDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? SourceName,
    VectorProtocolRange Range);

public sealed record VectorHostFailure(string Code, string Message);

public sealed class VectorExecutionResponse
{
    public bool Success { get; init; }

    public VectorExecutionEngine Engine { get; init; }

    public VectorExecutionOperation Operation { get; init; }

    public string[] Output { get; init; } = [];

    public string? Result { get; init; }

    public VectorProtocolDiagnostic[] Diagnostics { get; init; } = [];

    public string? Disassembly { get; init; }

    public VectorHostFailure? HostFailure { get; init; }
}

public static class ExecutionProtocolJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
