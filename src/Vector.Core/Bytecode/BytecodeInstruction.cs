using Vector.Core.Source;

namespace Vector.Core.Bytecode;

/// <summary>
/// One bytecode instruction with an optional integer operand and its originating source span.
/// </summary>
internal readonly record struct BytecodeInstruction(
    OpCode OpCode,
    int? Operand,
    SourceSpan Span)
{
    public BytecodeInstruction(OpCode opCode, SourceSpan span)
        : this(opCode, null, span)
    {
    }

    public BytecodeInstruction(OpCode opCode, int operand, SourceSpan span)
        : this(opCode, (int?)operand, span)
    {
    }

    public bool HasOperand => Operand.HasValue;

    public BytecodeInstruction WithOperand(int operand) =>
        new(OpCode, operand, Span);
}
