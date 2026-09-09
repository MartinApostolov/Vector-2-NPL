namespace Vector.LanguageServer;

public sealed record VectorLanguageServerOptions(bool LiveDiagnostics, string? ProgramRoot)
{
    public const string LiveDiagnosticsEnvironmentVariable = "VECTOR_LIVE_DIAGNOSTICS";
    public const string ProgramRootEnvironmentVariable = "VECTOR_PROGRAM_ROOT";

    public static VectorLanguageServerOptions Default { get; } = new(true, null);

    public static VectorLanguageServerOptions FromEnvironment() => FromValues(
        Environment.GetEnvironmentVariable(LiveDiagnosticsEnvironmentVariable),
        Environment.GetEnvironmentVariable(ProgramRootEnvironmentVariable));

    public static VectorLanguageServerOptions FromValues(string? liveDiagnostics, string? programRoot)
    {
        bool diagnosticsEnabled = !bool.TryParse(liveDiagnostics, out bool parsed) || parsed;
        string? normalizedRoot = NormalizeExistingDirectory(programRoot);
        return new VectorLanguageServerOptions(diagnosticsEnabled, normalizedRoot);
    }

    private static string? NormalizeExistingDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            string path = Path.GetFullPath(value.Trim());
            return Directory.Exists(path) ? path : null;
        }
        catch (Exception error) when (error is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return null;
        }
    }
}
