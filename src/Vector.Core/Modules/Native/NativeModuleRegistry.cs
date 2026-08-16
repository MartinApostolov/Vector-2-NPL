namespace Vector.Core.Modules.Native;

/// <summary>
/// Stores explicitly registered native Vector modules by their full qualified module id.
/// Registration is deliberate; the registry does not discover assemblies or modules through reflection.
/// </summary>
public sealed class NativeModuleRegistry
{
    private readonly Dictionary<ModuleId, NativeModuleDefinition> _definitions = new();

    public IReadOnlyCollection<NativeModuleDefinition> Definitions => _definitions.Values;

    public void Register(NativeModuleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_definitions.TryAdd(definition.Id, definition))
        {
            throw new InvalidOperationException(
                $"Native module '{definition.Id}' is already registered.");
        }
    }

    public bool TryGet(ModuleId moduleId, out NativeModuleDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        return _definitions.TryGetValue(moduleId, out definition);
    }
}
