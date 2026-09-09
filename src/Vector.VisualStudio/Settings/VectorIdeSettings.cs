namespace Vector.VisualStudio.Settings;

using Vector.ExecutionProtocol;

public sealed record VectorIdeSettings(
    VectorExecutionEngine DefaultEngine,
    bool LiveDiagnostics,
    string? ProgramRoot,
    IReadOnlyList<string> PluginPaths)
{
    public static VectorIdeSettings Default { get; } = new(
        VectorExecutionEngine.Interpreter,
        LiveDiagnostics: true,
        ProgramRoot: null,
        PluginPaths: []);

    public static VectorIdeSettings FromRaw(
        string? defaultEngine,
        bool liveDiagnostics,
        string? programRoot,
        string? pluginPaths) => new(
            string.Equals(defaultEngine, "vm", StringComparison.OrdinalIgnoreCase)
                ? VectorExecutionEngine.Vm
                : VectorExecutionEngine.Interpreter,
            liveDiagnostics,
            NormalizeOptional(programRoot),
            ParsePluginPaths(pluginPaths));

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> ParsePluginPaths(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(PathComparer)
            .ToArray();
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
