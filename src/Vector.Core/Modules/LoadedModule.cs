using Vector.Core.Modules.Native;
using Vector.Core.Syntax;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Core.Modules;

/// <summary>
/// Cached representation of one Vector module and its persistent top-level environment.
/// A module can be backed either by parsed Vector source or by an explicitly registered
/// native definition. One-time runtime initialization remains coordinated by
/// <see cref="ModuleLoader"/>.
/// </summary>
public sealed class LoadedModule
{
    private readonly ModuleId[] _imports;

    /// <summary>
    /// Creates a parsed local Vector source module.
    /// </summary>
    public LoadedModule(
        ModuleId id,
        SourceModuleData sourceData,
        RuntimeEnvironment environment,
        IEnumerable<ModuleId> imports)
        : this(
            id,
            ModuleKind.Source,
            environment,
            imports,
            sourceData ?? throw new ArgumentNullException(nameof(sourceData)),
            nativeDefinition: null)
    {
    }

    /// <summary>
    /// Creates a loaded representation of one explicitly registered native module.
    /// No source path, source text, or syntax tree is invented for native code.
    /// </summary>
    public LoadedModule(
        NativeModuleDefinition nativeDefinition,
        RuntimeEnvironment environment)
        : this(
            (nativeDefinition ?? throw new ArgumentNullException(nameof(nativeDefinition))).Id,
            ModuleKind.Native,
            environment,
            Array.Empty<ModuleId>(),
            sourceData: null,
            nativeDefinition: nativeDefinition)
    {
    }

    private LoadedModule(
        ModuleId id,
        ModuleKind kind,
        RuntimeEnvironment environment,
        IEnumerable<ModuleId> imports,
        SourceModuleData? sourceData,
        NativeModuleDefinition? nativeDefinition)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Kind = kind;
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        ArgumentNullException.ThrowIfNull(imports);
        _imports = imports.ToArray();
        SourceData = sourceData;
        NativeDefinition = nativeDefinition;
    }

    public ModuleId Id { get; }

    public string QualifiedNamespace => Id.QualifiedName;

    public ModuleKind Kind { get; }

    public RuntimeEnvironment Environment { get; }

    public IReadOnlyList<ModuleId> Imports => _imports;

    /// <summary>
    /// Source-only metadata. This is <see langword="null"/> for native modules.
    /// </summary>
    public SourceModuleData? SourceData { get; }

    /// <summary>
    /// Native-only definition. This is <see langword="null"/> for source modules.
    /// </summary>
    public NativeModuleDefinition? NativeDefinition { get; }

    // Source-module convenience properties retained for existing callers. Native modules
    // intentionally return null rather than receiving fake source metadata.
    public string? FilePath => SourceData?.FilePath;

    public string? Source => SourceData?.Source;

    public CompilationUnit? Syntax => SourceData?.Syntax;
}
