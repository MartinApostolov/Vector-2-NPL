namespace Vector.Plugins.Loading;

/// <summary>
/// Represents a controlled failure while loading or constructing an external Vector plugin.
/// </summary>
public sealed class VectorPluginLoadException : Exception
{
    internal VectorPluginLoadException(
        VectorPluginLoadErrorKind errorKind,
        string message,
        string? pluginPath = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorKind = errorKind;
        PluginPath = pluginPath;
    }

    public VectorPluginLoadErrorKind ErrorKind { get; }

    public string? PluginPath { get; }
}
