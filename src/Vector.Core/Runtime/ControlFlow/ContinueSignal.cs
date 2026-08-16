using Vector.Core.Source;

namespace Vector.Core.Runtime.ControlFlow;

internal sealed class ContinueSignal : Exception
{
    public ContinueSignal(SourceSpan span)
    {
        Span = span;
    }

    public SourceSpan Span { get; }
}
