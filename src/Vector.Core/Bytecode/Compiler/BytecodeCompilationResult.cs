using Vector.Core.Bytecode;

namespace Vector.Core.Bytecode.Compiler;

/// <summary>
/// Successful result of compiling one Vector syntax tree to bytecode.
/// </summary>
internal sealed class BytecodeCompilationResult
{
    public BytecodeCompilationResult(BytecodeProgram program)
    {
        Program = program ?? throw new ArgumentNullException(nameof(program));
    }

    public BytecodeProgram Program { get; }
}
