namespace Vector.LanguageServer.Protocol;

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;

public sealed class VectorInitializeResult
{
    [JsonProperty("capabilities")]
    public required ServerCapabilities Capabilities { get; init; }

    [JsonProperty("serverInfo")]
    public required VectorServerInfo ServerInfo { get; init; }
}

public sealed class VectorServerInfo
{
    [JsonProperty("name")]
    public required string Name { get; init; }

    [JsonProperty("version")]
    public required string Version { get; init; }
}
