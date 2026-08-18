namespace Vector.Core.Bytecode.Compiler;

/// <summary>
/// Tracks jump placeholders and lexical-scope depth for one active compiled loop.
/// </summary>
internal sealed class LoopCompilationContext
{
    public LoopCompilationContext(int baseScopeDepth)
    {
        if (baseScopeDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseScopeDepth));
        }

        BaseScopeDepth = baseScopeDepth;
    }

    public int BaseScopeDepth { get; }

    public List<int> BreakJumps { get; } = new();

    public List<int> ContinueJumps { get; } = new();
}
