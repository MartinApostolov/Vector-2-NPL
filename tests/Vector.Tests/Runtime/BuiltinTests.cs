using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Builtins;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Runtime;

public sealed class BuiltinTests
{
    [Fact]
    public void VectorHostForwardsEachLineToConfiguredSink()
    {
        var output = new List<string>();
        var host = new VectorHost(output.Add);

        host.WriteLine("first");
        host.WriteLine("second");

        Assert.Equal(new[] { "first", "second" }, output);
    }

    [Fact]
    public void DefaultVectorHostMayDiscardOutputWithoutThrowing()
    {
        var host = new VectorHost();

        host.WriteLine("ignored");
    }

    [Fact]
    public void InterpreterExposesConfiguredHost()
    {
        var host = new VectorHost();
        var interpreter = new Interpreter(host: host);

        Assert.Same(host, interpreter.Host);
    }

    [Fact]
    public void PrintIsAvailableAsRequiredGlobalBuiltin()
    {
        var value = Evaluate("print");

        var print = Assert.IsType<PrintBuiltin>(value);
        Assert.Equal("print", print.Name);
        Assert.Equal(1, print.Arity);
    }

    [Fact]
    public void PrintReturnsNothing()
    {
        var output = new List<string>();

        var result = Execute("print(42);", output);

        Assert.Same(NothingValue.Instance, result);
        Assert.Equal(new[] { "42" }, output);
    }

    [Fact]
    public void PrintWritesTextWithoutLiteralQuotes()
    {
        var output = new List<string>();

        Execute("print(\"Hello Vector\");", output);

        Assert.Equal(new[] { "Hello Vector" }, output);
    }

    [Theory]
    [InlineData("print(0);", "0")]
    [InlineData("print(12.5);", "12.5")]
    [InlineData("print(-3);", "-3")]
    [InlineData("print(true);", "true")]
    [InlineData("print(false);", "false")]
    [InlineData("print(nothing);", "nothing")]
    [InlineData("print([]);", "[]")]
    [InlineData("print([1, 2, 3]);", "[1, 2, 3]")]
    public void PrintFormatsCoreValues(string source, string expected)
    {
        var output = new List<string>();

        Execute(source, output);

        Assert.Equal(new[] { expected }, output);
    }

    [Fact]
    public void PrintFormatsNestedAndMixedListsRecursively()
    {
        var output = new List<string>();

        Execute("print([1, [\"two\", true], nothing]);", output);

        Assert.Equal(new[] { "[1, [two, true], nothing]" }, output);
    }

    [Fact]
    public void PrintFormatsFunctionValuesWithoutInvokingThem()
    {
        var output = new List<string>();

        Execute("function answer() { return 42; } print(answer);", output);

        Assert.Equal(new[] { "<function>" }, output);
    }

    [Fact]
    public void MultiplePrintCallsPreserveProgramOrder()
    {
        var output = new List<string>();

        Execute("print(1); print(2); print(3);", output);

        Assert.Equal(new[] { "1", "2", "3" }, output);
    }

    [Fact]
    public void PrintWorksInsideUserFunction()
    {
        var output = new List<string>();

        Execute("function show(value) { print(value); } show(9);", output);

        Assert.Equal(new[] { "9" }, output);
    }

    [Fact]
    public void PrintWorksInsideLoop()
    {
        var output = new List<string>();

        Execute("for item in [1, 2, 3] { print(item); }", output);

        Assert.Equal(new[] { "1", "2", "3" }, output);
    }

    [Fact]
    public void PrintBuiltinCanBeStoredAsFunctionValueAndCalled()
    {
        var output = new List<string>();

        Execute("let writer = print; writer(\"saved\");", output);

        Assert.Equal(new[] { "saved" }, output);
    }

    [Fact]
    public void LexicalBindingCanShadowBuiltinName()
    {
        var output = new List<string>();

        var result = Execute("let print = 5; print;", output);

        Assert.Equal(new NumberValue(5), result);
        Assert.Empty(output);
    }

    [Fact]
    public void BlockLocalShadowingDoesNotRemoveBuiltinAfterBlock()
    {
        var output = new List<string>();

        Execute("{ let print = 5; print; } print(7);", output);

        Assert.Equal(new[] { "7" }, output);
    }

    [Fact]
    public void PrintWithNoArgumentsReportsStructuredArityError()
    {
        var error = Assert.Throws<RuntimeError>(() => Execute("print();", new List<string>()));

        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, error.Code);
        Assert.Contains("1", error.Message);
        Assert.Contains("0", error.Message);
    }

    [Fact]
    public void PrintWithTooManyArgumentsReportsStructuredArityError()
    {
        var error = Assert.Throws<RuntimeError>(() => Execute("print(1, 2);", new List<string>()));

        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, error.Code);
        Assert.Contains("1", error.Message);
        Assert.Contains("2", error.Message);
    }

    [Fact]
    public void InvalidPrintArityIsRejectedBeforeArgumentsEvaluate()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("touched", new NumberValue(0), Span(0, 7));
        var output = new List<string>();

        Assert.Throws<RuntimeError>(() =>
            Execute("print(touched = 1, touched = 2);", output, environment));

        Assert.Equal(new NumberValue(0), environment.Get("touched", Span(0, 7)));
        Assert.Empty(output);
    }

    [Fact]
    public void CustomHostImplementationReceivesPrintOutput()
    {
        var host = new RecordingHost();
        var interpreter = new Interpreter(host: host);

        interpreter.Execute(Parse("print(\"custom\");"));

        Assert.Equal(new[] { "custom" }, host.Lines);
    }

    [Fact]
    public void SeparateInterpretersCanUseIndependentHosts()
    {
        var firstOutput = new List<string>();
        var secondOutput = new List<string>();
        var first = new Interpreter(host: new VectorHost(firstOutput.Add));
        var second = new Interpreter(host: new VectorHost(secondOutput.Add));

        first.Execute(Parse("print(1);"));
        second.Execute(Parse("print(2);"));

        Assert.Equal(new[] { "1" }, firstOutput);
        Assert.Equal(new[] { "2" }, secondOutput);
    }

    private static VectorValue Execute(
        string source,
        List<string> output,
        RuntimeEnvironment? environment = null)
    {
        var host = new VectorHost(output.Add);
        var interpreter = new Interpreter(environment, host);
        return interpreter.Execute(Parse(source));
    }

    private static VectorValue Evaluate(string source)
    {
        var parser = new Parser(new SourceText(source));
        var parseResult = parser.ParseExpression();
        Assert.Empty(parseResult.Diagnostics);
        return new Interpreter().Evaluate(parseResult.Root);
    }

    private static Vector.Core.Syntax.CompilationUnit Parse(string source)
    {
        var parser = new Parser(new SourceText(source));
        var parseResult = parser.ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }

    private static SourceSpan Span(int startOffset, int endOffset) =>
        new(
            new SourcePosition(startOffset, 1, startOffset + 1),
            new SourcePosition(endOffset, 1, endOffset + 1));

    private sealed class RecordingHost : IVectorHost
    {
        public List<string> Lines { get; } = new();

        public void WriteLine(string text) => Lines.Add(text);
    }
}
