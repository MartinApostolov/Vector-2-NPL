using Vector.Core.Modules;

namespace Vector.Plugins;

/// <summary>
/// Describes one plugin that has been successfully committed to a Vector native-module registry.
/// </summary>
public sealed class VectorPluginRegistration
{
    private readonly IReadOnlyList<ModuleId> _moduleIds;

    internal VectorPluginRegistration(string id, int apiVersion, IEnumerable<ModuleId> moduleIds)
    {
        Id = id;
        ApiVersion = apiVersion;
        _moduleIds = Array.AsReadOnly(moduleIds.ToArray());
    }

    public string Id { get; }

    public int ApiVersion { get; }

    public IReadOnlyList<ModuleId> ModuleIds => _moduleIds;
}
