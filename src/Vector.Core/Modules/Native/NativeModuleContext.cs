using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Core.Modules.Native;

/// <summary>
/// Provides the controlled initialization surface used by one registered native module.
/// Native modules export Vector runtime values into their persistent module environment.
/// </summary>
public sealed class NativeModuleContext
{
    private static readonly SourceSpan NativeDeclarationSpan = new(
        new SourcePosition(0, 1, 1),
        new SourcePosition(0, 1, 1));

    private readonly RuntimeEnvironment _environment;

    internal NativeModuleContext(RuntimeEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public void Export(string name, VectorValue value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A native module export name cannot be empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(value);
        _environment.Declare(name, value, NativeDeclarationSpan);
    }
}
