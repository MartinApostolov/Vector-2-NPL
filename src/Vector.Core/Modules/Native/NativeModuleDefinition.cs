using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Core.Modules.Native;

/// <summary>
/// Describes one explicitly registered C#/.NET-backed Vector module.
/// </summary>
public sealed class NativeModuleDefinition
{
    private readonly Action<NativeModuleContext> _initializer;

    public NativeModuleDefinition(ModuleId id, Action<NativeModuleContext> initializer)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
    }

    public ModuleId Id { get; }

    public string QualifiedNamespace => Id.QualifiedName;

    public void Initialize(RuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _initializer(new NativeModuleContext(environment));
    }
}
