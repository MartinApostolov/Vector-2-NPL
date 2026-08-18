using Vector.Core.Source;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Core.Bytecode.Vm;

/// <summary>
/// One explicit Vector VM call frame.
/// </summary>
internal sealed class VmCallFrame
{
    public VmCallFrame(
        BytecodeChunk chunk,
        RuntimeEnvironment environment,
        int stackBase,
        SourceSpan? callSpan = null)
    {
        Chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));

        if (stackBase < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stackBase));
        }

        StackBase = stackBase;
        CallSpan = callSpan;
    }

    public BytecodeChunk Chunk { get; }

    public RuntimeEnvironment Environment { get; set; }

    public int InstructionPointer { get; set; }

    public int StackBase { get; }

    public SourceSpan? CallSpan { get; }
}
