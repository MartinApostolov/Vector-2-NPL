using Vector.Core.Source;

namespace Vector.Core.Diagnostics;

/// <summary>
/// A structured language diagnostic with a stable code, severity, message, and source span.
/// </summary>
public sealed record Diagnostic
{
    public Diagnostic(
        DiagnosticCode code,
        string message,
        DiagnosticSeverity severity,
        SourceSpan span)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A diagnostic message cannot be empty.", nameof(message));
        }

        Code = code;
        Message = message;
        Severity = severity;
        Span = span;
    }

    public DiagnosticCode Code { get; }

    public string Message { get; }

    public DiagnosticSeverity Severity { get; }

    public SourceSpan Span { get; }
}
