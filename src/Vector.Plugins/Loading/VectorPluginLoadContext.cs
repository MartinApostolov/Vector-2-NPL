using System.Reflection;
using System.Runtime.Loader;
using Vector.Core.Modules.Native;

namespace Vector.Plugins.Loading;

/// <summary>
/// Isolates one plugin's private managed dependencies while sharing Vector's host contracts.
/// </summary>
internal sealed class VectorPluginLoadContext : AssemblyLoadContext
{
    private static readonly IReadOnlyDictionary<string, Assembly> SharedHostAssemblies =
        CreateSharedHostAssemblies();
    private static readonly HashSet<string> TrustedPlatformAssemblyNames =
        CreateTrustedPlatformAssemblyNames();

    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    public VectorPluginLoadContext(string pluginPath)
        : base($"VectorPlugin:{Path.GetFileNameWithoutExtension(pluginPath)}:{Guid.NewGuid():N}", isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _pluginDirectory = Path.GetDirectoryName(pluginPath)
            ?? throw new ArgumentException("Plugin path must have a parent directory.", nameof(pluginPath));
    }

    internal bool Owns(Assembly assembly) =>
        ReferenceEquals(GetLoadContext(assembly), this);

    /// <summary>
    /// Resolves one referenced assembly during the loader's eager dependency validation.
    /// Private plugin dependencies are deliberately loaded by path into this context instead
    /// of using LoadFromAssemblyName, which can fall back to an already-loaded Default-context
    /// assembly with the same identity.
    /// </summary>
    internal Assembly? LoadManagedDependency(AssemblyName assemblyName)
    {
        var simpleName = GetSimpleName(assemblyName);

        if (SharedHostAssemblies.TryGetValue(simpleName, out var sharedAssembly))
        {
            return sharedAssembly;
        }

        if (TrustedPlatformAssemblyNames.Contains(simpleName))
        {
            return null;
        }

        return LoadPluginPrivateAssembly(assemblyName, simpleName);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var simpleName = GetSimpleName(assemblyName);

        if (SharedHostAssemblies.TryGetValue(simpleName, out var sharedAssembly))
        {
            return sharedAssembly;
        }

        if (TrustedPlatformAssemblyNames.Contains(simpleName))
        {
            return null;
        }

        return LoadPluginPrivateAssembly(assemblyName, simpleName);
    }

    private Assembly LoadPluginPrivateAssembly(AssemblyName assemblyName, string simpleName)
    {
        var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolvedPath is not null)
        {
            return LoadFromAssemblyPath(resolvedPath);
        }

        var localPath = Path.Combine(_pluginDirectory, simpleName + ".dll");
        if (File.Exists(localPath))
        {
            return LoadFromAssemblyPath(localPath);
        }

        throw new FileNotFoundException(
            $"Managed dependency '{simpleName}' could not be resolved for the Vector plugin.",
            simpleName + ".dll");
    }

    private static string GetSimpleName(AssemblyName assemblyName)
    {
        var simpleName = assemblyName.Name;
        if (string.IsNullOrWhiteSpace(simpleName))
        {
            throw new FileLoadException("A referenced managed assembly has no usable simple name.");
        }

        return simpleName;
    }

    private static IReadOnlyDictionary<string, Assembly> CreateSharedHostAssemblies()
    {
        var assemblies = new[]
        {
            typeof(NativeModuleDefinition).Assembly,
            typeof(IVectorPlugin).Assembly
        };

        return assemblies.ToDictionary(
            assembly => assembly.GetName().Name!,
            assembly => assembly,
            StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> CreateTrustedPlatformAssemblyNames()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string trustedAssemblies)
        {
            return result;
        }

        foreach (var path in trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(name);
            }
        }

        return result;
    }
}
