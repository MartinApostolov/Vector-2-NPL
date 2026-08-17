using Vector.Cli;
using Vector.Tests.Plugins;
using Xunit;

namespace Vector.Tests.Repl;

public sealed class ReplPluginTests
{
    [Fact]
    public void ExplicitPluginIsAvailableWhenCliStartsRepl()
    {
        var plugin = PluginFixture.Assembly("Vector.TestPlugin", "Vector.TestPlugin.dll");
        var input = new StringReader(
            "import fixture.tools;\n" +
            "fixture.tools.double(21);\n" +
            "fixture.tools.double(5);\n" +
            ":exit\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(
            new[] { "--plugin", plugin },
            input,
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Vector REPL", output.ToString());
        Assert.Contains("42", output.ToString());
        Assert.Contains("10", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void MultipleExplicitPluginsRemainAvailableAcrossReplSubmissions()
    {
        var first = PluginFixture.Assembly("Vector.TestPlugin", "Vector.TestPlugin.dll");
        var second = PluginFixture.Assembly("Vector.TestPlugin.Second", "Vector.TestPlugin.Second.dll");
        var input = new StringReader(
            "import fixture.tools;\n" +
            "import fixture.extra;\n" +
            "fixture.tools.double(10);\n" +
            "fixture.extra.increment(10);\n" +
            "fixture.tools.double(fixture.extra.increment(20));\n" +
            ":quit\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(
            new[] { "--plugin", first, "--plugin", second },
            input,
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Contains("20", output.ToString());
        Assert.Contains("11", output.ToString());
        Assert.Contains("42", output.ToString());
        Assert.Empty(error.ToString());
    }
}
