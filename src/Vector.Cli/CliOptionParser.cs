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

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];

            if (string.Equals(argument, "--plugin", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Count
                    || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options = new CliOptions(pluginPaths, sourceFile);
                    error = "option '--plugin' requires a following DLL path.";
                    return false;
                }

                pluginPaths.Add(args[++index]);
                continue;
            }

            if (argument.StartsWith("-", StringComparison.Ordinal))
            {
                options = new CliOptions(pluginPaths, sourceFile);
                error = $"unknown option '{argument}'.";
                return false;
            }

            if (sourceFile is not null)
            {
                options = new CliOptions(pluginPaths, sourceFile);
                error = "at most one Vector source file may be supplied.";
                return false;
            }

            sourceFile = argument;
        }

        options = new CliOptions(pluginPaths, sourceFile);
        error = null;
        return true;
    }
}
