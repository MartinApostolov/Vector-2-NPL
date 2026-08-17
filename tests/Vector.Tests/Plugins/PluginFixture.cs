namespace Vector.Tests.Plugins;

internal static class PluginFixture
{
    public static string Assembly(string projectName, string assemblyName)
    {
        var root = FindRepositoryRoot();
        var configuration = GetBuildConfiguration();
        var path = Path.Combine(
            root,
            "tests",
            projectName,
            "bin",
            configuration,
            "net8.0",
            assemblyName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Plugin fixture assembly '{path}' was not built.",
                path);
        }

        return path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vector.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Vector.sln above test output '{AppContext.BaseDirectory}'.");
    }

    private static string GetBuildConfiguration()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = output.Parent?.Name;
        return string.IsNullOrWhiteSpace(configuration) ? "Debug" : configuration;
    }
}
