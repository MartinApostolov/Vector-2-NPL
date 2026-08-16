using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Execution;

/// <summary>
/// Result of one high-level Vector source execution.
/// </summary>
public sealed class ExecutionResult
{
    private readonly Diagnostic[] _diagnostics;
    private readonly string[] _output;

    public ExecutionResult(
        VectorValue? result,
        IEnumerable<Diagnostic> diagnostics,
        IEnumerable<string> output)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(output);

        Result = result;
        _diagnostics = diagnostics.ToArray();
        _output = output.ToArray();
    }

    /// <summary>
    /// Final program value, or null when execution could not complete successfully.
    /// </summary>
    public VectorValue? Result { get; }

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Lines written through the Vector host during this execution.
    /// </summary>
    public IReadOnlyList<string> Output => _output;

    public bool Success => !_diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
