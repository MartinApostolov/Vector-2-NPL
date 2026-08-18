namespace Vector.Cli;

internal static class CliOptionParser
{
    public static bool TryParse(
        IReadOnlyList<string> args,
        out CliOptions options,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        var pluginPaths = new List<string>();
        string? sourceFile = null;
        var engine = CliExecutionEngine.Interpreter;
        var engineSpecified = false;
        var disassemble = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];

            if (string.Equals(argument, "--plugin", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Count
                    || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options = new CliOptions(pluginPaths, sourceFile, engine, disassemble);
                    error = "option '--plugin' requires a following DLL path.";
                    return false;
                }

                pluginPaths.Add(args[++index]);
                continue;
            }

            if (string.Equals(argument, "--engine", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Count
                    || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options = new CliOptions(pluginPaths, sourceFile, engine, disassemble);
                    error = "option '--engine' requires 'interpreter' or 'vm'.";
                    return false;
                }

                if (engineSpecified)
                {
                    options = new CliOptions(pluginPaths, sourceFile, engine, disassemble);
                    error = "option '--engine' may only be supplied once.";
                    return false;
                }

                var engineName = args[++index];
                if (string.Equals(engineName, "interpreter", StringComparison.OrdinalIgnoreCase))
                {
                    engine = CliExecutionEngine.Interpreter;
                }
                else if (string.Equals(engineName, "vm", StringComparison.OrdinalIgnoreCase))
                {
                    engine = CliExecutionEngine.Vm;
                }
                else
                {
                    options = new CliOptions(pluginPaths, sourceFile, engine, disassemble);
                    error = $"invalid engine '{engineName}'; expected 'interpreter' or 'vm'.";
                    return false;
                }

                engineSpecified = true;
                continue;
            }

            if (string.Equals(argument, "--disassemble", StringComparison.Ordinal))
            {
                if (disassemble)
                {
                    options = new CliOptions(pluginPaths, sourceFile, engine, disassemble);
                    error = "option '--disassemble' may only be supplied once.";
                    return false;
                }

                disassemble = true;
                continue;
            }

            if (argument.StartsWith("-", StringComparison.Ordinal))
            {
                options = new CliOptions(pluginPaths, sourceFile, engine, disassemble);
                error = $"unknown option '{argument}'.";
                return false;
            }

            if (sourceFile is not null)
            {
                options = new CliOptions(pluginPaths, sourceFile, engine, disassemble);
                error = "at most one Vector source file may be supplied.";
                return false;
            }

            sourceFile = argument;
        }

        if (disassemble && engine != CliExecutionEngine.Vm)
        {
            options = new CliOptions(pluginPaths, sourceFile, engine, disassemble);
            error = "option '--disassemble' requires '--engine vm'.";
            return false;
        }

        if (disassemble && sourceFile is null)
        {
            options = new CliOptions(pluginPaths, sourceFile, engine, disassemble);
            error = "option '--disassemble' requires a Vector source file.";
            return false;
        }

        options = new CliOptions(pluginPaths, sourceFile, engine, disassemble);
        error = null;
        return true;
    }
}
