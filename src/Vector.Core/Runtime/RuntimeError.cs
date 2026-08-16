using Vector.Core.Diagnostics;
using Vector.Core.Source;

namespace Vector.Core.Runtime;

/// <summary>
/// Represents a runtime language failure with a stable diagnostic code and exact source span.
/// </summary>
public sealed class RuntimeError : Exception
{
    public RuntimeError(DiagnosticCode code, string message, SourceSpan span)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A runtime error message cannot be empty.", nameof(message));
        }

        Code = code;
        Span = span;
    }

    public DiagnosticCode Code { get; }

    public SourceSpan Span { get; }
}
