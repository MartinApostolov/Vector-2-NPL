using Vector.Core.Source;

namespace Vector.Core.Diagnostics;

/// <summary>
/// A structured language diagnostic with a stable code, severity, message, source span,
/// and optional source identity for diagnostics that originate outside the caller's fallback source.
/// </summary>
public sealed record Diagnostic
{
    public Diagnostic(
        DiagnosticCode code,
        string message,
        DiagnosticSeverity severity,
        SourceSpan span,
        string? sourceName = null,
        string? sourceText = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A diagnostic message cannot be empty.", nameof(message));
        }

        if (sourceName is not null && string.IsNullOrWhiteSpace(sourceName))
        {
            throw new ArgumentException("A diagnostic source name cannot be empty.", nameof(sourceName));
        }

        Code = code;
        Message = message;
        Severity = severity;
        Span = span;
        SourceName = sourceName;
        SourceText = sourceText;
    }

    public DiagnosticCode Code { get; }

    public string Message { get; }

    public DiagnosticSeverity Severity { get; }

    public SourceSpan Span { get; }

    /// <summary>
    /// Display name/path of the source that produced this diagnostic. Null means the
    /// consumer should retain its fallback source name.
    /// </summary>
    public string? SourceName { get; }

    /// <summary>
    /// Source text that produced this diagnostic. Null means the consumer should retain
    /// its fallback source text.
    /// </summary>
    public string? SourceText { get; }

    public Diagnostic WithSource(string? sourceName, string sourceText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        return new Diagnostic(Code, Message, Severity, Span, sourceName, sourceText);
    }
}
