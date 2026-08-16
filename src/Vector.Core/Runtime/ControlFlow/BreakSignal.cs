using Vector.Core.Source;

namespace Vector.Core.Runtime.ControlFlow;

internal sealed class BreakSignal : Exception
{
    public BreakSignal(SourceSpan span)
    {
        Span = span;
    }

    public SourceSpan Span { get; }
}
