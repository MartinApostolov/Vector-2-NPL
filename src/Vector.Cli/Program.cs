using System.Text;
using Vector.Core;
using Vector.Core.Runtime.Host;

namespace Vector.Cli;

internal static class Program
{
    private const int SuccessExitCode = 0;
    private const int LanguageFailureExitCode = 1;
    private const int CommandLineFailureExitCode = 2;

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return new Repl().Run();
        }

        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: vector [file.vec]");
            return CommandLineFailureExitCode;
        }

        string filePath;
        try
        {
            filePath = Path.GetFullPath(args[0]);
        }
        catch (Exception error) when (
            error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"vector: cannot resolve source path: {error.Message}");
            return CommandLineFailureExitCode;
        }

        if (!string.Equals(Path.GetExtension(filePath), ".vec", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("vector: source file must use the .vec extension.");
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
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            Console.Error.WriteLine($"vector: cannot read '{filePath}': {error.Message}");
            return CommandLineFailureExitCode;
        }

        var programRoot = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var host = new VectorInputHost(Console.WriteLine, Console.ReadLine);
        var result = new VectorEngine().Execute(source, programRoot, host);

        if (result.Success)
        {
            return SuccessExitCode;
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine(CliDiagnosticFormatter.Format(diagnostic, filePath, source));
        }

        return LanguageFailureExitCode;
    }
}
