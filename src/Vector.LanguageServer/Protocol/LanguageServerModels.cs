namespace Vector.LanguageServer.Protocol;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record InitializeParams(
    [property: JsonPropertyName("processId")] int? ProcessId,
    [property: JsonPropertyName("rootUri")] string? RootUri,
    [property: JsonPropertyName("capabilities")] JsonElement Capabilities);

public sealed record InitializeResult(
    [property: JsonPropertyName("capabilities")] ServerCapabilities Capabilities,
    [property: JsonPropertyName("serverInfo")] ServerInfo ServerInfo);

public sealed record ServerCapabilities;

public sealed record ServerInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version);
