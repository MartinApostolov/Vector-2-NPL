using Vector.Core.Modules.Native;

namespace Vector.Plugins;

/// <summary>
/// Provides the controlled registration surface available to external Vector plugins.
/// </summary>
public interface IVectorPluginContext
{
    /// <summary>
    /// Stages one native Vector module for registration by the plugin host.
    /// </summary>
    void RegisterModule(NativeModuleDefinition definition);
}
