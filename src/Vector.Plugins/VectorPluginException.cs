using Vector.Core.Modules;

namespace Vector.Plugins;

/// <summary>
/// Represents a controlled failure while registering a Vector plugin.
/// </summary>
public sealed class VectorPluginException : Exception
{
    internal VectorPluginException(
        VectorPluginErrorKind errorKind,
        string message,
        string? pluginId = null,
        ModuleId? moduleId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorKind = errorKind;
        PluginId = pluginId;
        ModuleId = moduleId;
    }

    public VectorPluginErrorKind ErrorKind { get; }

    public string? PluginId { get; }

    public ModuleId? ModuleId { get; }
}
