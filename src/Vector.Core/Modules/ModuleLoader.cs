using System.Text;
using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;
using Vector.Core.Source;
using Vector.Core.Syntax.Statements;

namespace Vector.Core.Modules;

/// <summary>
/// Resolves, parses, initializes, and caches local Vector modules for one program execution.
/// Plain loading remains parse-only; importing performs one-time top-level initialization.
/// </summary>
public sealed class ModuleLoader
{
    private readonly Dictionary<ModuleId, LoadedModule> _loaded = new();
    private readonly HashSet<ModuleId> _loading = new();
    private readonly List<ModuleId> _loadingStack = new();
    private readonly HashSet<ModuleId> _initialized = new();
    private readonly HashSet<ModuleId> _initializing = new();

    public ModuleLoader(ModuleResolver resolver)
    {
        Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public ModuleResolver Resolver { get; }

    public IReadOnlyCollection<LoadedModule> LoadedModules => _loaded.Values;

    public IReadOnlyCollection<LoadedModule> InitializedModules =>
        _loaded.Values.Where(module => _initialized.Contains(module.Id)).ToArray();

    public LoadedModule Load(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        if (_loaded.TryGetValue(moduleId, out var cached))
        {
            return cached;
        }

        if (_loading.Contains(moduleId))
        {
            throw CreateCircularImportError(moduleId);
        }

        _loading.Add(moduleId);
        _loadingStack.Add(moduleId);

        try
        {
            var filePath = Resolver.Resolve(moduleId);
            if (!File.Exists(filePath))
            {
                throw new ModuleLoadException(
                    ModuleLoadErrorKind.ModuleNotFound,
                    moduleId,
                    filePath,
                    $"Module '{moduleId}' was not found at '{filePath}'.");
            }

            string source;
            try
            {
                source = File.ReadAllText(filePath, Encoding.UTF8);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new ModuleLoadException(
                    ModuleLoadErrorKind.IoFailure,
                    moduleId,
                    filePath,
                    $"Module '{moduleId}' could not be read: {exception.Message}",
                    innerException: exception);
            }

            var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
            if (parseResult.HasErrors)
            {
                var diagnostics = parseResult.Diagnostics
                    .Select(diagnostic => diagnostic.WithSource(filePath, source))
                    .ToArray();
                throw new ModuleLoadException(
                    ModuleLoadErrorKind.InvalidSyntax,
                    moduleId,
                    filePath,
                    $"Module '{moduleId}' contains syntax errors.",
                    diagnostics);
            }

            var imports = parseResult.Root.Statements
                .OfType<ImportStatement>()
                .Select(ModuleId.FromImport)
                .ToArray();

            foreach (var dependency in imports)
            {
                Load(dependency);
            }

            var loaded = new LoadedModule(
                moduleId,
                filePath,
                source,
                parseResult.Root,
                new RuntimeEnvironment(),
                imports);

            _loaded.Add(moduleId, loaded);
            return loaded;
        }
        finally
        {
            _loading.Remove(moduleId);
            _loadingStack.RemoveAt(_loadingStack.Count - 1);
        }
    }

    /// <summary>
    /// Loads a module if necessary and executes its top-level code exactly once for this loader.
    /// Imported dependencies initialize through the module's own import statements.
    /// </summary>
    public LoadedModule Import(ModuleId moduleId, IVectorHost host)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(host);

        var module = Load(moduleId);
        Initialize(module, host);
        return module;
    }

    public bool IsInitialized(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        return _initialized.Contains(moduleId);
    }

    internal bool TryGetModuleForEnvironment(RuntimeEnvironment environment, out LoadedModule? module)
    {
        ArgumentNullException.ThrowIfNull(environment);

        for (RuntimeEnvironment? scope = environment; scope is not null; scope = scope.Enclosing)
        {
            module = _loaded.Values.FirstOrDefault(
                candidate => ReferenceEquals(candidate.Environment, scope));
            if (module is not null)
            {
                return true;
            }
        }

        module = null;
        return false;
    }

    private void Initialize(LoadedModule module, IVectorHost host)
    {
        if (_initialized.Contains(module.Id))
        {
            return;
        }

        if (!_initializing.Add(module.Id))
        {
            throw new InvalidOperationException(
                $"Module '{module.Id}' is already being initialized.");
        }

        try
        {
            var interpreter = new Interpreter(module.Environment, host, this);
            interpreter.Execute(module.Syntax, module.FilePath, module.Source);
            _initialized.Add(module.Id);
        }
        catch (RuntimeError error)
        {
            throw error.WithSource(module.FilePath, module.Source);
        }
        finally
        {
            _initializing.Remove(module.Id);
        }
    }

    public bool TryGetLoaded(ModuleId moduleId, out LoadedModule? module)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        return _loaded.TryGetValue(moduleId, out module);
    }

    private ModuleLoadException CreateCircularImportError(ModuleId repeated)
    {
        var cycleStart = _loadingStack.FindIndex(id => id.Equals(repeated));
        var cycle = _loadingStack
            .Skip(cycleStart < 0 ? 0 : cycleStart)
            .Append(repeated)
            .ToArray();
        var path = Resolver.Resolve(repeated);

        return new ModuleLoadException(
            ModuleLoadErrorKind.CircularImport,
            repeated,
            path,
            $"Circular module import detected: {string.Join(" -> ", cycle.Select(id => id.QualifiedName))}.",
            cycle: cycle);
    }
}

/// <summary>
/// Stable categories for module-resolution and module-loading failures.
/// </summary>
public enum ModuleLoadErrorKind
{
    ModuleNotFound,
    InvalidSyntax,
    CircularImport,
    IoFailure
}

/// <summary>
/// Structured module-loading failure retaining the module id, file, parse diagnostics, and cycle when applicable.
/// </summary>
public sealed class ModuleLoadException : Exception
{
    private readonly Diagnostic[] _diagnostics;
    private readonly ModuleId[] _cycle;

    public ModuleLoadException(
        ModuleLoadErrorKind kind,
        ModuleId moduleId,
        string filePath,
        string message,
        IEnumerable<Diagnostic>? diagnostics = null,
        IEnumerable<ModuleId>? cycle = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A module file path is required.", nameof(filePath));
        }

        FilePath = Path.GetFullPath(filePath);
        _diagnostics = diagnostics?.ToArray() ?? Array.Empty<Diagnostic>();
        _cycle = cycle?.ToArray() ?? Array.Empty<ModuleId>();
    }

    public ModuleLoadErrorKind Kind { get; }

    public ModuleId ModuleId { get; }

    public string FilePath { get; }

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public IReadOnlyList<ModuleId> Cycle => _cycle;
}
