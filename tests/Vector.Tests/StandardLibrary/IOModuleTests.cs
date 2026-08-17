using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Vector.Core.StandardLibrary;
using Vector.Core.StandardLibrary.IO;
using Xunit;

namespace Vector.Tests.StandardLibrary;

public sealed class IOModuleTests
{
    [Fact]
    public void DefaultStandardLibraryRegistersLibIO()
    {
        var registry = StandardLibraryRegistry.CreateDefault();

        Assert.True(registry.TryGet(IOModule.Id, out var definition));
        Assert.NotNull(definition);
        Assert.Equal("lib.io", definition!.QualifiedNamespace);
    }

    [Fact]
    public void ReadLineReturnsAvailableText()
    {
        var host = new VectorInputHost(null, () => "hello");

        var result = new VectorEngine().Execute(
            "import lib.io; lib.io.readLine();",
            host: host);

        Assert.True(result.Success);
        Assert.Equal(new TextValue("hello"), result.Result);
    }

    [Fact]
    public void ReadLinePreservesOrdinarySpaces()
    {
        var host = new VectorInputHost(null, () => "  hello world  ");

        var result = new VectorEngine().Execute(
            "import lib.io; lib.io.readLine();",
            host: host);

        Assert.True(result.Success);
        Assert.Equal(new TextValue("  hello world  "), result.Result);
    }

    [Fact]
    public void ReadLineReturnsNothingAtEndOfInput()
    {
        var host = new VectorInputHost(null, () => null);

        var result = new VectorEngine().Execute(
            "import lib.io; lib.io.readLine();",
            host: host);

        Assert.True(result.Success);
        Assert.Same(NothingValue.Instance, result.Result);
    }

    [Theory]
    [InlineData("lib.io.readLine(1);")]
    [InlineData("lib.io.readLine(1, 2);")]
    public void ReadLineUsesStrictZeroArgumentArity(string expression)
    {
        var host = new VectorInputHost(null, () => "unused");

        var result = new VectorEngine().Execute(
            $"import lib.io; {expression}",
            host: host);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void OutputOnlyHostProducesStructuredUnsupportedInputFailure()
    {
        var result = new VectorEngine().Execute(
            "import lib.io; lib.io.readLine();",
            host: new VectorHost());

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Contains("input-capable", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingHostInputCapabilityAlsoProducesStructuredFailure()
    {
        var result = new VectorEngine().Execute("import lib.io; lib.io.readLine();");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ImportIsRequiredForQualifiedIOAccess()
    {
        var host = new VectorInputHost(null, () => "unused");

        var result = new VectorEngine().Execute("lib.io.readLine();", host: host);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ImportDoesNotLeakReadLineAsUnqualifiedGlobal()
    {
        var host = new VectorInputHost(null, () => "unused");

        var result = new VectorEngine().Execute(
            "import lib.io; readLine();",
            host: host);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }
}
