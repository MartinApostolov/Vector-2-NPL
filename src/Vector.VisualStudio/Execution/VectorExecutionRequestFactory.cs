namespace Vector.VisualStudio.Execution;

using Vector.ExecutionProtocol;
using Vector.VisualStudio.Settings;

public static class VectorExecutionRequestFactory
{
    public static VectorExecutionPreparation Create(
        string source,
        string? sourcePath,
        VectorIdeSettings settings,
        VectorExecutionEngine? engineOverride = null,
        VectorExecutionOperation operation = VectorExecutionOperation.Run)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);

        string? programRoot;
        try
        {
            programRoot = ResolveProgramRoot(sourcePath, settings.ProgramRoot);
        }
        catch (Exception error) when (error is ArgumentException
            or IOException
            or NotSupportedException
            or PathTooLongException)
        {
            return VectorExecutionPreparation.Invalid("invalid_program_root", error.Message);
        }

        if (settings.ProgramRoot is not null && !Directory.Exists(programRoot))
        {
            return VectorExecutionPreparation.Invalid(
                "invalid_program_root",
                $"The configured Vector program root does not exist: '{programRoot}'.");
        }

        string[] pluginPaths;
        try
        {
            pluginPaths = operation == VectorExecutionOperation.Run
                ? settings.PluginPaths.Select(Path.GetFullPath).ToArray()
                : [];
        }
        catch (Exception error) when (error is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return VectorExecutionPreparation.Invalid("invalid_plugin_path", error.Message);
        }

        return VectorExecutionPreparation.Valid(new VectorExecutionRequest
        {
            Source = source,
            SourcePath = sourcePath,
            ProgramRoot = programRoot,
            Engine = engineOverride ?? settings.DefaultEngine,
            Operation = operation,
            PluginPaths = pluginPaths,
        });
    }

    private static string? ResolveProgramRoot(string? sourcePath, string? configuredRoot)
    {
        if (configuredRoot is not null)
        {
            return Path.GetFullPath(configuredRoot);
        }

        return sourcePath is null ? null : Path.GetDirectoryName(Path.GetFullPath(sourcePath));
    }
}

public sealed record VectorExecutionPreparation(
    VectorExecutionRequest? Request,
    VectorHostFailure? Failure)
{
    public static VectorExecutionPreparation Valid(VectorExecutionRequest request) => new(request, null);

    public static VectorExecutionPreparation Invalid(string code, string message) =>
        new(null, new VectorHostFailure(code, message));
}
