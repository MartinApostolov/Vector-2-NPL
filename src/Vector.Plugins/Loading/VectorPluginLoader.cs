using System.Reflection;

namespace Vector.Plugins.Loading;

/// <summary>
/// Loads one explicitly selected .NET assembly and constructs its single Vector plugin entry point.
/// </summary>
public sealed class VectorPluginLoader
{
    public IVectorPlugin LoadFromPath(string path)
    {
        var fullPath = NormalizePluginPath(path);
        var loadContext = new VectorPluginLoadContext(fullPath);
        var assembly = LoadAssembly(fullPath, loadContext);
        ValidateManagedDependencies(assembly, loadContext, fullPath);
        var entryType = DiscoverEntryType(assembly, fullPath);
        return CreatePlugin(entryType, fullPath);
    }

    private static string NormalizePluginPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new VectorPluginLoadException(
                VectorPluginLoadErrorKind.InvalidPath,
                "Plugin path must be a non-empty value.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new VectorPluginLoadException(
                VectorPluginLoadErrorKind.InvalidPath,
                $"Plugin path '{path}' is invalid.",
                path,
                exception);
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new VectorPluginLoadException(
                VectorPluginLoadErrorKind.InvalidExtension,
                $"Plugin path '{fullPath}' must identify a .dll file.",
                fullPath);
        }

        if (!File.Exists(fullPath))
        {
            throw new VectorPluginLoadException(
                VectorPluginLoadErrorKind.FileNotFound,
                $"Plugin assembly '{fullPath}' does not exist.",
                fullPath);
        }

        return fullPath;
    }

    private static Assembly LoadAssembly(string fullPath, VectorPluginLoadContext loadContext)
    {
        try
        {
            return loadContext.LoadFromAssemblyPath(fullPath);
        }
        catch (Exception exception) when (IsAssemblyLoadException(exception))
        {
            throw AssemblyLoadFailure(fullPath, exception);
        }
    }

    private static void ValidateManagedDependencies(
        Assembly rootAssembly,
        VectorPluginLoadContext loadContext,
        string fullPath)
    {
        var pending = new Queue<Assembly>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Enqueue(rootAssembly);

        while (pending.Count > 0)
        {
            var assembly = pending.Dequeue();
            var identity = assembly.FullName ?? assembly.GetName().Name ?? assembly.Location;
            if (!visited.Add(identity))
            {
                continue;
            }

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                try
                {
                    var dependency = loadContext.LoadManagedDependency(reference);
                    if (dependency is not null && loadContext.Owns(dependency))
                    {
                        pending.Enqueue(dependency);
                    }
                }
                catch (Exception exception) when (IsAssemblyLoadException(exception))
                {
                    throw AssemblyLoadFailure(fullPath, exception);
                }
            }
        }
    }

    private static Type DiscoverEntryType(Assembly assembly, string fullPath)
    {
        Type[] assemblyTypes;
        try
        {
            assemblyTypes = assembly.GetTypes();
        }
        catch (Exception exception) when (
            exception is ReflectionTypeLoadException
                or FileLoadException
                or FileNotFoundException
                or TypeLoadException)
        {
            throw AssemblyLoadFailure(fullPath, exception);
        }

        var entryTypes = assemblyTypes
            .Where(type =>
                type.IsClass
                && type.IsPublic
                && typeof(IVectorPlugin).IsAssignableFrom(type))
            .ToArray();

        if (entryTypes.Length == 0)
        {
            throw new VectorPluginLoadException(
                VectorPluginLoadErrorKind.NoPluginEntryPoint,
                $"Plugin assembly '{fullPath}' does not contain a public Vector plugin entry point.",
                fullPath);
        }

        if (entryTypes.Length > 1)
        {
            throw new VectorPluginLoadException(
                VectorPluginLoadErrorKind.MultiplePluginEntryPoints,
                $"Plugin assembly '{fullPath}' contains more than one public Vector plugin entry point.",
                fullPath);
        }

        var entryType = entryTypes[0];
        if (entryType.IsAbstract
            || entryType.ContainsGenericParameters
            || entryType.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new VectorPluginLoadException(
                VectorPluginLoadErrorKind.InvalidPluginEntryPoint,
                $"Vector plugin entry point '{entryType.FullName}' must be a concrete public type with a public parameterless constructor.",
                fullPath);
        }

        return entryType;
    }

    private static IVectorPlugin CreatePlugin(Type entryType, string fullPath)
    {
        try
        {
            var instance = Activator.CreateInstance(entryType);
            if (instance is not IVectorPlugin plugin)
            {
                throw new InvalidOperationException(
                    $"Entry point '{entryType.FullName}' did not produce an IVectorPlugin instance.");
            }

            return plugin;
        }
        catch (TargetInvocationException exception)
        {
            throw new VectorPluginLoadException(
                VectorPluginLoadErrorKind.ConstructorFailure,
                $"Vector plugin entry point '{entryType.FullName}' failed during construction.",
                fullPath,
                exception.InnerException ?? exception);
        }
        catch (Exception exception) when (
            exception is MemberAccessException
                or MissingMethodException
                or InvalidOperationException)
        {
            throw new VectorPluginLoadException(
                VectorPluginLoadErrorKind.ConstructorFailure,
                $"Vector plugin entry point '{entryType.FullName}' could not be constructed.",
                fullPath,
                exception);
        }
    }

    private static bool IsAssemblyLoadException(Exception exception) =>
        exception is BadImageFormatException
            or FileLoadException
            or FileNotFoundException
            or TypeLoadException;

    private static VectorPluginLoadException AssemblyLoadFailure(string fullPath, Exception exception) =>
        new(
            VectorPluginLoadErrorKind.AssemblyLoadFailure,
            $"Plugin assembly '{fullPath}' or one of its managed dependencies could not be loaded.",
            fullPath,
            exception);
}
