using System.Reflection;
using System.Runtime.Loader;

namespace Vector.Plugins.Loading;

/// <summary>
/// Loads one explicitly selected .NET assembly and constructs its single Vector plugin entry point.
/// </summary>
public sealed class VectorPluginLoader
{
    public IVectorPlugin LoadFromPath(string path)
    {
        var fullPath = NormalizePluginPath(path);
        var assembly = LoadAssembly(fullPath);
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

    private static Assembly LoadAssembly(string fullPath)
    {
        try
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException
                or FileLoadException
                or FileNotFoundException)
        {
            throw AssemblyLoadFailure(fullPath, exception);
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
                or FileNotFoundException)
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

    private static VectorPluginLoadException AssemblyLoadFailure(string fullPath, Exception exception) =>
        new(
            VectorPluginLoadErrorKind.AssemblyLoadFailure,
            $"Plugin assembly '{fullPath}' could not be loaded as a compatible .NET assembly.",
            fullPath,
            exception);
}
