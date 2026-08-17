using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Plugins.Loading;

namespace Vector.Plugins;

/// <summary>
/// Validates and transactionally commits already-instantiated Vector plugins.
/// </summary>
public sealed class VectorPluginManager
{
    private readonly NativeModuleRegistry _nativeModules;
    private readonly Dictionary<string, VectorPluginRegistration> _registrationsById =
        new(StringComparer.Ordinal);
    private readonly List<VectorPluginRegistration> _registrations = new();

    public VectorPluginManager(NativeModuleRegistry nativeModules)
    {
        _nativeModules = nativeModules ?? throw new ArgumentNullException(nameof(nativeModules));
    }

    public IReadOnlyList<VectorPluginRegistration> Registrations => _registrations.AsReadOnly();

    /// <summary>
    /// Loads one explicitly selected plugin assembly and registers its single plugin entry point.
    /// </summary>
    public VectorPluginRegistration LoadFromPath(string path)
    {
        var plugin = new VectorPluginLoader().LoadFromPath(path);
        return Register(plugin);
    }

    public VectorPluginRegistration Register(IVectorPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        var pluginId = plugin.Id;
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new VectorPluginException(
                VectorPluginErrorKind.InvalidPluginId,
                "Plugin id must be a non-empty value.",
                pluginId);
        }

        if (_registrationsById.ContainsKey(pluginId))
        {
            throw new VectorPluginException(
                VectorPluginErrorKind.DuplicatePlugin,
                $"Plugin '{pluginId}' is already registered.",
                pluginId);
        }

        var apiVersion = plugin.ApiVersion;
        if (apiVersion != VectorPluginApi.CurrentVersion)
        {
            throw new VectorPluginException(
                VectorPluginErrorKind.ApiVersionMismatch,
                $"Plugin '{pluginId}' targets Vector plugin API version {apiVersion}, " +
                $"but this host supports version {VectorPluginApi.CurrentVersion}.",
                pluginId);
        }

        var context = new VectorPluginContext();

        try
        {
            plugin.Register(context);
        }
        catch (Exception exception)
        {
            var duplicateModuleId = context.DuplicateModuleId;
            if (duplicateModuleId is not null)
            {
                throw DuplicateModuleFailure(pluginId, duplicateModuleId, exception);
            }

            throw new VectorPluginException(
                VectorPluginErrorKind.RegistrationFailure,
                $"Plugin '{pluginId}' failed while registering its modules.",
                pluginId,
                innerException: exception);
        }

        // A plugin could catch the context's duplicate-staging exception itself. The host
        // still rejects that registration so duplicate module ids cannot be silently ignored.
        var swallowedDuplicateModuleId = context.DuplicateModuleId;
        if (swallowedDuplicateModuleId is not null)
        {
            throw DuplicateModuleFailure(pluginId, swallowedDuplicateModuleId);
        }

        foreach (var definition in context.StagedModules)
        {
            if (_nativeModules.TryGet(definition.Id, out _))
            {
                throw new VectorPluginException(
                    VectorPluginErrorKind.ModuleConflict,
                    $"Plugin '{pluginId}' cannot register module '{definition.Id}' because that module is already registered.",
                    pluginId,
                    definition.Id);
            }
        }

        foreach (var definition in context.StagedModules)
        {
            _nativeModules.Register(definition);
        }

        var registration = new VectorPluginRegistration(
            pluginId,
            apiVersion,
            context.StagedModules.Select(definition => definition.Id));

        _registrationsById.Add(pluginId, registration);
        _registrations.Add(registration);
        return registration;
    }

    private static VectorPluginException DuplicateModuleFailure(
        string pluginId,
        ModuleId moduleId,
        Exception? innerException = null) =>
        new(
            VectorPluginErrorKind.DuplicateModule,
            $"Plugin '{pluginId}' registers module '{moduleId}' more than once.",
            pluginId,
            moduleId,
            innerException);
}
