using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Builtins;

/// <summary>
/// Creates the standard global builtin bindings shared by Vector execution backends.
/// </summary>
internal static class BuiltinRegistry
{
    public static IReadOnlyDictionary<string, VectorValue> Create(IVectorHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return new Dictionary<string, VectorValue>(StringComparer.Ordinal)
        {
            ["print"] = new PrintBuiltin(host),
            ["length"] = new LengthBuiltin(),
            ["concat"] = new ConcatBuiltin(),
            ["text"] = new TextBuiltin(),
            ["number"] = new NumberBuiltin(),
            ["type"] = new TypeBuiltin(),
            ["range"] = new RangeBuiltin()
        };
    }
}
