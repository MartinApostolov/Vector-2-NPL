using Vector.Core;
using Vector.Core.Execution;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Host;
using Vector.Core.StandardLibrary;

namespace Vector.Plugins;

/// <summary>
/// Provides a straightforward embedded-host setup that shares one native-module registry
/// between external plugins and the Vector execution engine.
/// </summary>
public sealed class VectorPluginRuntime
{
    private VectorPluginRuntime(
        NativeModuleRegistry nativeModules,
        VectorPluginManager plugins)
    {
        NativeModules = nativeModules;
        Plugins = plugins;
        Engine = new VectorEngine(nativeModules);
    }

    /// <summary>
    /// Gets the shared native-module registry used by both plugins and the engine.
    /// </summary>
    public NativeModuleRegistry NativeModules { get; }

    /// <summary>
    /// Gets the plugin manager bound to <see cref="NativeModules"/>.
    /// </summary>
    public VectorPluginManager Plugins { get; }

    /// <summary>
    /// Gets the Vector engine bound to <see cref="NativeModules"/>.
    /// </summary>
    public VectorEngine Engine { get; }

    /// <summary>
    /// Creates a runtime with Vector's standard library and loads every explicitly supplied plugin
    /// before the runtime is returned to the embedding host.
    /// </summary>
    public static VectorPluginRuntime CreateDefault(params string[] pluginPaths)
    {
        ArgumentNullException.ThrowIfNull(pluginPaths);

        var nativeModules = StandardLibraryRegistry.CreateDefault();
        var plugins = new VectorPluginManager(nativeModules);

        foreach (var pluginPath in pluginPaths)
        {
            plugins.LoadFromPath(pluginPath);
        }

        return new VectorPluginRuntime(nativeModules, plugins);
    }

    /// <summary>
    /// Executes Vector source through the shared engine.
    /// </summary>
    public ExecutionResult Execute(
        string source,
        string? programRoot = null,
        IVectorHost? host = null) =>
        Engine.Execute(source, programRoot, host);
}
