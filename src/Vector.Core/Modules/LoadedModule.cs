using RuntimeEnvironment = Vector.Core.Runtime.Environment;
using Vector.Core.Syntax;

namespace Vector.Core.Modules;

/// <summary>
/// Parsed, cached representation of one local Vector module and its persistent top-level environment.
/// One-time runtime initialization is coordinated by <see cref="ModuleLoader"/>.
/// </summary>
public sealed class LoadedModule
{
    private readonly ModuleId[] _imports;

    public LoadedModule(
        ModuleId id,
        string filePath,
        CompilationUnit syntax,
        RuntimeEnvironment environment,
        IEnumerable<ModuleId> imports)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A module file path is required.", nameof(filePath));
        }

        FilePath = Path.GetFullPath(filePath);
        Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        ArgumentNullException.ThrowIfNull(imports);
        _imports = imports.ToArray();
    }

    public ModuleId Id { get; }

    public string QualifiedNamespace => Id.QualifiedName;

    public string FilePath { get; }

    public CompilationUnit Syntax { get; }

    public RuntimeEnvironment Environment { get; }

    public IReadOnlyList<ModuleId> Imports => _imports;
}
