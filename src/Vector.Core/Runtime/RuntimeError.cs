using Vector.Core.Diagnostics;
using Vector.Core.Source;

namespace Vector.Core.Runtime;

/// <summary>
/// Represents a runtime language failure with a stable diagnostic code and exact source span.
/// </summary>
public sealed class RuntimeError : Exception
{
    public RuntimeError(
        DiagnosticCode code,
        string message,
        SourceSpan span,
        string? sourceName = null,
        string? sourceText = null)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A runtime error message cannot be empty.", nameof(message));
        }

        if (sourceName is not null && string.IsNullOrWhiteSpace(sourceName))
        {
            throw new ArgumentException("A runtime error source name cannot be empty.", nameof(sourceName));
        }

        Code = code;
        Span = span;
        SourceName = sourceName;
        SourceText = sourceText;
    }

    public DiagnosticCode Code { get; }

    public SourceSpan Span { get; }

    public string? SourceName { get; }

    public string? SourceText { get; }

    public RuntimeError WithSource(string? sourceName, string sourceText)
    {
        if (SourceText is not null)
        {
            return this;
        }

        ArgumentNullException.ThrowIfNull(sourceText);
        return new RuntimeError(Code, Message, Span, sourceName, sourceText);
    }
}
