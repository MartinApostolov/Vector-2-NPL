using System.Collections;
using Vector.Core.Source;

namespace Vector.Core.Diagnostics;

/// <summary>
/// Mutable diagnostic collection shared by language-processing stages.
/// </summary>
public sealed class DiagnosticBag : IReadOnlyList<Diagnostic>
{
    private readonly List<Diagnostic> _diagnostics = new();

    public int Count => _diagnostics.Count;

    public Diagnostic this[int index] => _diagnostics[index];

    public bool HasErrors => _diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    public void Add(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _diagnostics.Add(diagnostic);
    }

    public Diagnostic Report(
        DiagnosticCode code,
        string message,
        DiagnosticSeverity severity,
        SourceSpan span)
    {
        var diagnostic = new Diagnostic(code, message, severity, span);
        Add(diagnostic);
        return diagnostic;
    }

    public void AddRange(IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        foreach (var diagnostic in diagnostics)
        {
            Add(diagnostic);
        }
    }

    public IEnumerator<Diagnostic> GetEnumerator() => _diagnostics.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
