namespace Vector.Core.Modules;

/// <summary>
/// Maps qualified Vector module ids to local .vec files under one program root.
/// </summary>
public sealed class ModuleResolver
{
    public ModuleResolver(string programRoot)
    {
        if (string.IsNullOrWhiteSpace(programRoot))
        {
            throw new ArgumentException("A program root path is required.", nameof(programRoot));
        }

        ProgramRoot = Path.GetFullPath(programRoot);
    }

    public string ProgramRoot { get; }

    public string Resolve(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        var parts = moduleId.Segments.ToArray();
        parts[^1] += ".vec";

        var relativePath = Path.Combine(parts);
        var resolvedPath = Path.GetFullPath(Path.Combine(ProgramRoot, relativePath));

        var relativeToRoot = Path.GetRelativePath(ProgramRoot, resolvedPath);
        if (relativeToRoot == ".."
            || relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativeToRoot))
        {
            throw new InvalidOperationException(
                $"Resolved module '{moduleId}' would escape the program root.");
        }

        return resolvedPath;
    }
}
