using System.Text;
using Vector.Core.Runtime.Host;
using Vector.Plugins;
using Vector.Plugins.Loading;

namespace Vector.Cli;

internal static class Program
{
    private const int SuccessExitCode = 0;
    private const int LanguageFailureExitCode = 1;
    private const int CommandLineFailureExitCode = 2;
    private const string Usage = "Usage: vector [--plugin plugin.dll]... [file.vec]";

    public static int Main(string[] args) =>
        Run(args, Console.In, Console.Out, Console.Error);

    internal static int Run(
        IReadOnlyList<string> args,
        TextReader input,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!CliOptionParser.TryParse(args, out var options, out var parseError))
        {
            error.WriteLine($"vector: {parseError}");
            error.WriteLine(Usage);
            return CommandLineFailureExitCode;
        }

        var runtime = VectorPluginRuntime.CreateDefault();
        foreach (var pluginPath in options.PluginPaths)
        {
            try
            {
                runtime.Plugins.LoadFromPath(pluginPath);
            }
            catch (VectorPluginLoadException pluginError)
            {
                error.WriteLine(
                    $"vector: plugin '{pluginPath}' could not be loaded: {pluginError.Message}");
                return CommandLineFailureExitCode;
            }
            catch (VectorPluginException pluginError)
            {
                error.WriteLine(
                    $"vector: plugin '{pluginPath}' could not be registered: {pluginError.Message}");
                return CommandLineFailureExitCode;
            }
            catch (Exception pluginError)
            {
                error.WriteLine(
                    $"vector: plugin '{pluginPath}' failed during setup: {pluginError.Message}");
                return CommandLineFailureExitCode;
            }
        }

        if (options.SourceFile is null)
        {
            return new Repl(
                input,
                output,
                error,
                nativeModules: runtime.NativeModules).Run();
        }

        string filePath;
        try
        {
            filePath = Path.GetFullPath(options.SourceFile);
        }
        catch (Exception pathError) when (
            pathError is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error.WriteLine($"vector: cannot resolve source path: {pathError.Message}");
            return CommandLineFailureExitCode;
        }

        if (!string.Equals(Path.GetExtension(filePath), ".vec", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine("vector: source file must use the .vec extension.");
            return CommandLineFailureExitCode;
        }

        string source;
        try
        {
            var utf8 = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);
            source = File.ReadAllText(filePath, utf8);
        }
        catch (Exception readError) when (
            readError is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            error.WriteLine($"vector: cannot read '{filePath}': {readError.Message}");
            return CommandLineFailureExitCode;
        }

        var programRoot = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var host = new VectorInputHost(output.WriteLine, input.ReadLine);
        var result = runtime.Execute(source, programRoot, host);

        if (result.Success)
        {
            return SuccessExitCode;
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            error.WriteLine(CliDiagnosticFormatter.Format(diagnostic, filePath, source));
        }

        return LanguageFailureExitCode;
    }
}
