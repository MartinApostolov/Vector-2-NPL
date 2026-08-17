using Vector.Core.Modules;
using Vector.Core.Modules.Native;

namespace Vector.Plugins;

/// <summary>
/// Collects plugin module definitions without mutating a runtime registry.
/// </summary>
internal sealed class VectorPluginContext : IVectorPluginContext
{
    private readonly List<NativeModuleDefinition> _stagedModules = new();
    private readonly HashSet<ModuleId> _stagedModuleIds = new();

    internal IReadOnlyList<NativeModuleDefinition> StagedModules => _stagedModules;

    internal ModuleId? DuplicateModuleId { get; private set; }

    public void RegisterModule(NativeModuleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_stagedModuleIds.Add(definition.Id))
        {
            DuplicateModuleId ??= definition.Id;
            throw new InvalidOperationException(
                $"Plugin module '{definition.Id}' has already been staged.");
        }

        _stagedModules.Add(definition);
    }
}
