namespace Vector.Plugins.Loading;

/// <summary>
/// Identifies a stable category of failure while loading an external Vector plugin assembly.
/// </summary>
public enum VectorPluginLoadErrorKind
{
    InvalidPath,
    FileNotFound,
    InvalidExtension,
    AssemblyLoadFailure,
    NoPluginEntryPoint,
    MultiplePluginEntryPoints,
    InvalidPluginEntryPoint,
    ConstructorFailure
}
