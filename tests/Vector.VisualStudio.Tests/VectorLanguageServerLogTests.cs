namespace Vector.VisualStudio.Tests;

using System.Diagnostics;
using Vector.VisualStudio.Diagnostics;
using Xunit;

public sealed class VectorLanguageServerLogTests
{
    [Fact]
    public void LifecycleLog_PreservesStartupStderrAndExitDiagnostics()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"vector-lsp-log-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "language-server.log");

        try
        {
            var traceSource = new TraceSource("VectorLanguageServerLogTests", SourceLevels.All);
            try
            {
                var log = new VectorLanguageServerLog(traceSource, path);
                log.Information("executable='C:\\extension\\OutOfProc\\LanguageServer\\Vector.LanguageServer.exe'");
                log.Error("stderr: simulated startup failure; exit code 23; beforeInitializeCompleted=True");
            }
            finally
            {
                traceSource.Close();
            }

            string contents = File.ReadAllText(path);
            Assert.Contains("OutOfProc\\LanguageServer\\Vector.LanguageServer.exe", contents, StringComparison.Ordinal);
            Assert.Contains("stderr: simulated startup failure", contents, StringComparison.Ordinal);
            Assert.Contains("exit code 23", contents, StringComparison.Ordinal);
            Assert.Contains("beforeInitializeCompleted=True", contents, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
