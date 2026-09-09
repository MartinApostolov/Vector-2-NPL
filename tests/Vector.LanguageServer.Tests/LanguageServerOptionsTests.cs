namespace Vector.LanguageServer.Tests;

using Xunit;

public sealed class LanguageServerOptionsTests
{
    [Fact]
    public void FromValues_DefaultsDiagnosticsOnAndProgramRootToAuto()
    {
        VectorLanguageServerOptions options = VectorLanguageServerOptions.FromValues(null, null);

        Assert.True(options.LiveDiagnostics);
        Assert.Null(options.ProgramRoot);
    }

    [Fact]
    public void FromValues_ParsesDiagnosticsAndExistingProgramRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "vector-options-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            VectorLanguageServerOptions options = VectorLanguageServerOptions.FromValues("false", root);

            Assert.False(options.LiveDiagnostics);
            Assert.Equal(Path.GetFullPath(root), options.ProgramRoot);
        }
        finally
        {
            Directory.Delete(root);
        }
    }

    [Fact]
    public void FromValues_IgnoresInvalidProgramRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "missing-vector-options-" + Guid.NewGuid().ToString("N"));

        VectorLanguageServerOptions options = VectorLanguageServerOptions.FromValues("invalid", root);

        Assert.True(options.LiveDiagnostics);
        Assert.Null(options.ProgramRoot);
    }
}
