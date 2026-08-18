using Vector.Core.Modules;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;

namespace Vector.Core.Bytecode;

/// <summary>
/// Builds one bytecode chunk while assigning stable indexes to its pools and jump targets.
/// </summary>
internal sealed class BytecodeBuilder
{
    private const int UnpatchedJumpTarget = -1;

    private readonly List<BytecodeInstruction> _instructions = new();
    private readonly List<VectorValue> _constants = new();
    private readonly List<string> _names = new();
    private readonly Dictionary<string, int> _nameIndexes = new(StringComparer.Ordinal);
    private readonly List<ModuleId> _modules = new();
    private readonly Dictionary<ModuleId, int> _moduleIndexes = new();
    private readonly List<BytecodeFunctionPrototype> _functions = new();

    public int InstructionCount => _instructions.Count;

    public int AddConstant(VectorValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var index = _constants.Count;
        _constants.Add(value);
        return index;
    }

    public int AddName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A bytecode name cannot be empty.", nameof(name));
        }

        if (_nameIndexes.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var index = _names.Count;
        _names.Add(name);
        _nameIndexes.Add(name, index);
        return index;
    }

    public int AddModule(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        if (_moduleIndexes.TryGetValue(moduleId, out var existing))
        {
            return existing;
        }

        var index = _modules.Count;
        _modules.Add(moduleId);
        _moduleIndexes.Add(moduleId, index);
        return index;
    }

    public int AddFunction(BytecodeFunctionPrototype function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var index = _functions.Count;
        _functions.Add(function);
        return index;
    }

    public int Emit(OpCode opCode, SourceSpan span)
    {
        var index = _instructions.Count;
        _instructions.Add(new BytecodeInstruction(opCode, span));
        return index;
    }

    public int Emit(OpCode opCode, int operand, SourceSpan span)
    {
        var index = _instructions.Count;
        _instructions.Add(new BytecodeInstruction(opCode, operand, span));
        return index;
    }

    public int EmitJump(OpCode opCode, SourceSpan span)
    {
        if (!IsJump(opCode))
        {
            throw new ArgumentException(
                $"Opcode '{opCode}' is not a jump instruction.",
                nameof(opCode));
        }

        return Emit(opCode, UnpatchedJumpTarget, span);
    }

    public void PatchJumpToCurrent(int instructionIndex) =>
        PatchJump(instructionIndex, _instructions.Count);

    public void PatchJump(int instructionIndex, int targetInstructionIndex)
    {
        if (instructionIndex < 0 || instructionIndex >= _instructions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(instructionIndex));
        }

        if (targetInstructionIndex < 0 || targetInstructionIndex > _instructions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(targetInstructionIndex));
        }

        var instruction = _instructions[instructionIndex];
        if (!IsJump(instruction.OpCode))
        {
            throw new InvalidOperationException(
                $"Instruction {instructionIndex} is '{instruction.OpCode}', not a jump instruction.");
        }

        if (instruction.Operand != UnpatchedJumpTarget)
        {
            throw new InvalidOperationException(
                $"Jump instruction {instructionIndex} has already been patched.");
        }

        _instructions[instructionIndex] = instruction.WithOperand(targetInstructionIndex);
    }

    public BytecodeChunk Build(string? sourceName = null, string? sourceText = null)
    {
        var unpatchedIndex = _instructions.FindIndex(
            instruction => IsJump(instruction.OpCode) && instruction.Operand == UnpatchedJumpTarget);

        if (unpatchedIndex >= 0)
        {
            throw new InvalidOperationException(
                $"Jump instruction {unpatchedIndex} has not been patched.");
        }

        return new BytecodeChunk(
            _instructions,
            _constants,
            _names,
            _modules,
            _functions,
            sourceName,
            sourceText);
    }

    private static bool IsJump(OpCode opCode) =>
        opCode is OpCode.Jump or OpCode.JumpIfFalse or OpCode.JumpIfTrue;
}
