using Vector.Core.Syntax;

namespace Vector.Core.Modules;

/// <summary>
/// Source-only metadata for a local <c>.vec</c> module.
/// Native modules deliberately do not fabricate any of these values.
/// </summary>
public sealed class SourceModuleData
{
    public SourceModuleData(string filePath, string source, CompilationUnit syntax)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A source module file path is required.", nameof(filePath));
        }

        ArgumentNullException.ThrowIfNull(source);
        Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));

        FilePath = Path.GetFullPath(filePath);
        Source = source;
    }

    public string FilePath { get; }

    public string Source { get; }

    public CompilationUnit Syntax { get; }
}
