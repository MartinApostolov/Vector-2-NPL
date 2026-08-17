namespace Vector.Plugins;

/// <summary>
/// Defines the entry contract implemented by one external Vector plugin.
/// </summary>
public interface IVectorPlugin
{
    /// <summary>
    /// Gets the stable identity of this plugin.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the Vector plugin API version targeted by this plugin.
    /// </summary>
    int ApiVersion { get; }

    /// <summary>
    /// Registers the Vector modules exported by this plugin.
    /// </summary>
    void Register(IVectorPluginContext context);
}
