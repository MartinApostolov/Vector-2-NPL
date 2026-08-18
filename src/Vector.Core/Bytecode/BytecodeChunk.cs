using Vector.Core.Modules;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Bytecode;

/// <summary>
/// Immutable compiled instruction stream plus the pools referenced by its operands.
/// </summary>
internal sealed class BytecodeChunk
{
    private readonly IReadOnlyList<BytecodeInstruction> _instructions;
    private readonly IReadOnlyList<VectorValue> _constants;
    private readonly IReadOnlyList<string> _names;
    private readonly IReadOnlyList<ModuleId> _modules;
    private readonly IReadOnlyList<BytecodeFunctionPrototype> _functions;

    public BytecodeChunk(
        IEnumerable<BytecodeInstruction> instructions,
        IEnumerable<VectorValue> constants,
        IEnumerable<string> names,
        IEnumerable<ModuleId> modules,
        IEnumerable<BytecodeFunctionPrototype> functions,
        string? sourceName = null,
        string? sourceText = null)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(functions);

        _instructions = Array.AsReadOnly(instructions.ToArray());
        _constants = Array.AsReadOnly(constants.ToArray());
        _names = Array.AsReadOnly(names.ToArray());
        _modules = Array.AsReadOnly(modules.ToArray());
        _functions = Array.AsReadOnly(functions.ToArray());
        SourceName = sourceName;
        SourceText = sourceText;
    }

    public IReadOnlyList<BytecodeInstruction> Instructions => _instructions;

    public IReadOnlyList<VectorValue> Constants => _constants;

    public IReadOnlyList<string> Names => _names;

    public IReadOnlyList<ModuleId> Modules => _modules;

    public IReadOnlyList<BytecodeFunctionPrototype> Functions => _functions;

    public string? SourceName { get; }

    public string? SourceText { get; }
}
