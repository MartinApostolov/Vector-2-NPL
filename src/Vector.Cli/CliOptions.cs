namespace Vector.Cli;

internal sealed class CliOptions
{
    public CliOptions(
        IEnumerable<string> pluginPaths,
        string? sourceFile,
        CliExecutionEngine engine = CliExecutionEngine.Interpreter,
        bool disassemble = false)
    {
        PluginPaths = Array.AsReadOnly(pluginPaths.ToArray());
        SourceFile = sourceFile;
        Engine = engine;
        Disassemble = disassemble;
    }

    public IReadOnlyList<string> PluginPaths { get; }

    public string? SourceFile { get; }

    public CliExecutionEngine Engine { get; }

    public bool Disassemble { get; }
}
