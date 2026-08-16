using Vector.Core.Runtime.Values;
using Vector.Core.Source;

namespace Vector.Core.Runtime.ControlFlow;

internal sealed class ReturnSignal : Exception
{
    public ReturnSignal(VectorValue value, SourceSpan span)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Span = span;
    }

    public VectorValue Value { get; }

    public SourceSpan Span { get; }
}
