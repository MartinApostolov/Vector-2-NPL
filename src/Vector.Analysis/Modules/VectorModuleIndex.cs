namespace Vector.Analysis.Modules;

using System.Text;
using Vector.Analysis.Documents;
using Vector.Analysis.Symbols;
using Vector.Core.Modules;
using Vector.Core.Syntax.Statements;

public sealed class VectorModuleIndex
{
    private readonly object gate = new();
    private readonly VectorAnalyzer analyzer;
    private readonly Dictionary<string, VectorAnalysisResult> openDocuments = new(PathComparer);
    private readonly Dictionary<string, string> inheritedRoots = new(PathComparer);
    private readonly Dictionary<ModuleCacheKey, CacheEntry> cache = new(ModuleCacheKeyComparer.Instance);

    public VectorModuleIndex(VectorAnalyzer? analyzer = null)
    {
        this.analyzer = analyzer ?? new VectorAnalyzer();
    }

    public bool TryGetImportedModule(
        VectorAnalysisResult importingDocument,
        string qualifiedName,
        out VectorModuleAnalysis? module,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(importingDocument);
        ArgumentException.ThrowIfNullOrEmpty(qualifiedName);
        cancellationToken.ThrowIfCancellationRequested();

        bool imported = importingDocument.Syntax.Statements.OfType<ImportStatement>()
            .Any(statement => statement.QualifiedPath == qualifiedName);
        if (!imported || TryCreateModuleId(qualifiedName) is not ModuleId id)
        {
            module = null;
            return false;
        }

        string? root = GetProgramRoot(importingDocument.Document);
        if (root is null)
        {
            module = null;
            return false;
        }

        string normalizedRoot = NormalizePath(root);
        string filePath = new ModuleResolver(normalizedRoot).Resolve(id);
        lock (this.gate)
        {
            this.inheritedRoots[filePath] = normalizedRoot;
            if (this.openDocuments.TryGetValue(filePath, out VectorAnalysisResult? open))
            {
                module = CreateModule(id, filePath, open);
                return true;
            }
        }

        string source;
        try
        {
            if (!File.Exists(filePath))
            {
                this.RemoveCached(normalizedRoot, id);
                module = null;
                return false;
            }

            source = File.ReadAllText(filePath, Encoding.UTF8);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            this.RemoveCached(normalizedRoot, id);
            module = null;
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var key = new ModuleCacheKey(normalizedRoot, id.QualifiedName);
        VectorAnalysisResult analysis;
        lock (this.gate)
        {
            if (this.cache.TryGetValue(key, out CacheEntry? cached)
                && string.Equals(cached.Source, source, StringComparison.Ordinal))
            {
                module = cached.Module;
                return true;
            }

            int version = this.cache.TryGetValue(key, out cached) ? cached.Version + 1 : 0;
            VectorDocumentSnapshot snapshot = VectorDocumentSnapshot.FromFile(
                filePath,
                source,
                version,
                normalizedRoot);
            analysis = this.analyzer.Analyze(snapshot, cancellationToken);
            module = CreateModule(id, filePath, analysis);
            this.cache[key] = new CacheEntry(source, version, module);
            return true;
        }
    }

    internal void TrackOpenDocument(VectorAnalysisResult analysis)
    {
        if (analysis.Document.FilePath is not string filePath)
        {
            return;
        }

        lock (this.gate)
        {
            this.openDocuments[NormalizePath(filePath)] = analysis;
        }
    }

    internal void UntrackOpenDocument(VectorAnalysisResult analysis)
    {
        if (analysis.Document.FilePath is not string filePath)
        {
            return;
        }

        lock (this.gate)
        {
            this.openDocuments.Remove(NormalizePath(filePath));
        }
    }

    public string? GetInheritedProgramRoot(string filePath)
    {
        lock (this.gate)
        {
            return this.inheritedRoots.GetValueOrDefault(NormalizePath(filePath));
        }
    }

    private void RemoveCached(string root, ModuleId id)
    {
        lock (this.gate)
        {
            this.cache.Remove(new ModuleCacheKey(root, id.QualifiedName));
        }
    }

    private static VectorModuleAnalysis CreateModule(ModuleId id, string filePath, VectorAnalysisResult analysis)
    {
        VectorSymbol[] members = analysis.SemanticModel.RootScope.Declarations
            .Where(symbol => !symbol.IsDuplicate && !symbol.IsSynthetic)
            .ToArray();
        return new VectorModuleAnalysis(id, filePath, analysis, members);
    }

    private static string? GetProgramRoot(VectorDocumentSnapshot document)
    {
        if (!string.IsNullOrWhiteSpace(document.ProgramRoot))
        {
            return document.ProgramRoot;
        }

        return document.FilePath is null ? null : Path.GetDirectoryName(document.FilePath);
    }

    private static ModuleId? TryCreateModuleId(string qualifiedName)
    {
        try
        {
            return new ModuleId(qualifiedName.Split('.'));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private sealed record CacheEntry(string Source, int Version, VectorModuleAnalysis Module);

    private sealed record ModuleCacheKey(string Root, string QualifiedName);

    private sealed class ModuleCacheKeyComparer : IEqualityComparer<ModuleCacheKey>
    {
        public static ModuleCacheKeyComparer Instance { get; } = new();

        public bool Equals(ModuleCacheKey? left, ModuleCacheKey? right) =>
            ReferenceEquals(left, right)
            || (left is not null
                && right is not null
                && PathComparer.Equals(left.Root, right.Root)
                && StringComparer.Ordinal.Equals(left.QualifiedName, right.QualifiedName));

        public int GetHashCode(ModuleCacheKey value) => HashCode.Combine(
            PathComparer.GetHashCode(value.Root),
            StringComparer.Ordinal.GetHashCode(value.QualifiedName));
    }
}
