namespace Vector.Cli;

internal sealed class CliOptions
{
    public CliOptions(IEnumerable<string> pluginPaths, string? sourceFile)
    {
        PluginPaths = Array.AsReadOnly(pluginPaths.ToArray());
        SourceFile = sourceFile;
    }

    public IReadOnlyList<string> PluginPaths { get; }

    public string? SourceFile { get; }
}
