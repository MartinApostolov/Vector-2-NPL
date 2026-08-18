using Vector.Core.Bytecode;
using Vector.Core.Diagnostics;

namespace Vector.Core.Execution;

/// <summary>
/// Result of compiling one Vector source submission for the bytecode VM.
/// Successful results expose deterministic human-readable disassembly for debugging.
/// </summary>
public sealed class VmCompilationResult
{
    private readonly Diagnostic[] _diagnostics;

    internal VmCompilationResult(
        BytecodeProgram? program,
        IEnumerable<Diagnostic> diagnostics,
        string? disassembly)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        Program = program;
        _diagnostics = diagnostics.ToArray();
        Disassembly = disassembly;
    }

    /// <summary>
    /// Structured lexer/parser diagnostics produced before bytecode generation.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Stable human-readable bytecode disassembly, or null when compilation could not complete.
    /// </summary>
    public string? Disassembly { get; }

    /// <summary>
    /// True when the source compiled to executable VM bytecode without error diagnostics.
    /// </summary>
    public bool Success =>
        Program is not null &&
        !_diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    internal BytecodeProgram? Program { get; }
}
