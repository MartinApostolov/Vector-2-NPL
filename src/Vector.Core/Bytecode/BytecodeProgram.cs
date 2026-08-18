namespace Vector.Core.Bytecode;

/// <summary>
/// Root compiled representation of one Vector entry program.
/// </summary>
internal sealed class BytecodeProgram
{
    public BytecodeProgram(BytecodeChunk entryPoint)
    {
        EntryPoint = entryPoint ?? throw new ArgumentNullException(nameof(entryPoint));
    }

    public BytecodeChunk EntryPoint { get; }
}
