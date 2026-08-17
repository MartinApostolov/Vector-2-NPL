namespace Vector.Plugins;

/// <summary>
/// Identifies a stable category of plugin registration failure.
/// </summary>
public enum VectorPluginErrorKind
{
    InvalidPluginId,
    ApiVersionMismatch,
    DuplicatePlugin,
    DuplicateModule,
    ModuleConflict,
    RegistrationFailure
}
