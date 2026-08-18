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
    public void BuiltinRegistryCreatesTheCompleteSharedBuiltinSetForAHost()
    {
        var output = new List<string>();
        var host = new VectorHost(output.Add);
        var builtins = BuiltinRegistry.Create(host);

        Assert.Equal(7, builtins.Count);
        Assert.IsType<PrintBuiltin>(builtins["print"]);
        Assert.IsType<LengthBuiltin>(builtins["length"]);
        Assert.IsType<ConcatBuiltin>(builtins["concat"]);
        Assert.IsType<TextBuiltin>(builtins["text"]);
        Assert.IsType<NumberBuiltin>(builtins["number"]);
        Assert.IsType<TypeBuiltin>(builtins["type"]);
        Assert.IsType<RangeBuiltin>(builtins["range"]);

        var interpreter = new Interpreter(host: host);
        var print = Assert.IsType<PrintBuiltin>(builtins["print"]);
        print.Call(interpreter, new VectorValue[] { new NumberValue(42) });

        Assert.Equal(new[] { "42" }, output);
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

        Assert.Equal(new[] { "[1, [\"two\", true], nothing]" }, output);
    }

    [Fact]
    public void DisplayEscapesTextNestedInsideLists()
    {
        var value = new ListValue(new VectorValue[]
        {
            new TextValue("quote: \""),
            new TextValue("slash: \\"),
            new TextValue("line\nbreak"),
            new TextValue("tab\tstop")
        });

        Assert.Equal(
            """["quote: \"", "slash: \\", "line\nbreak", "tab\tstop"]""",
            VectorValueFormatter.Format(value));
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

    [Theory]
    [InlineData("length", 1)]
    [InlineData("concat", 2)]
    [InlineData("text", 1)]
    [InlineData("number", 1)]
    [InlineData("type", 1)]
    [InlineData("range", 2)]
    public void EssentialBuiltinsAreAvailableGlobally(string name, int arity)
    {
        var builtin = Assert.IsAssignableFrom<BuiltinFunction>(Evaluate(name));

        Assert.Equal(name, builtin.Name);
        Assert.Equal(arity, builtin.Arity);
    }

    [Fact]
    public void LengthReturnsListElementCount()
    {
        Assert.Equal(new NumberValue(4), Execute("length([1, \"two\", true, nothing]);", new List<string>()));
    }

    [Fact]
    public void LengthCountsUnicodeScalarValuesInText()
    {
        Assert.Equal(new NumberValue(3), Execute("length(\"A😀B\");", new List<string>()));
    }

    [Fact]
    public void LengthRejectsUnsupportedRuntimeTypes()
    {
        var error = Assert.Throws<RuntimeError>(() => Execute("length(12);", new List<string>()));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("text or list", error.Message);
    }

    [Fact]
    public void ConcatReturnsNewListWithBothInputsInOrder()
    {
        var result = Execute("concat([1, 2], [3, 4]);", new List<string>());

        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(1), new NumberValue(2), new NumberValue(3), new NumberValue(4)
            }),
            result);
    }

    [Fact]
    public void ConcatSupportsMixedAndNestedListValues()
    {
        var result = Execute("concat([1, \"two\"], [[3], nothing]);", new List<string>());

        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(1),
                new TextValue("two"),
                new ListValue(new VectorValue[] { new NumberValue(3) }),
                NothingValue.Instance
            }),
            result);
    }

    [Fact]
    public void ConcatDoesNotMutateEitherInputList()
    {
        var environment = new RuntimeEnvironment();

        var result = Execute(
            "let left = [1]; let right = [2]; let joined = concat(left, right); " +
            "joined[0] = 9; [left, right, joined];",
            new List<string>(),
            environment);

        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[] { new NumberValue(1) }),
                new ListValue(new VectorValue[] { new NumberValue(2) }),
                new ListValue(new VectorValue[] { new NumberValue(9), new NumberValue(2) })
            }),
            result);
    }

    [Theory]
    [InlineData("concat(1, []);")]
    [InlineData("concat([], 1);")]
    [InlineData("concat(\"a\", \"b\");")]
    public void ConcatRequiresTwoLists(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Execute(source, new List<string>()));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("two lists", error.Message);
    }

    [Fact]
    public void TextReturnsExistingTextValue()
    {
        Assert.Equal(new TextValue("Vector"), Execute("text(\"Vector\");", new List<string>()));
    }

    [Theory]
    [InlineData("text(12.5);", "12.5")]
    [InlineData("text(true);", "true")]
    [InlineData("text(false);", "false")]
    [InlineData("text(nothing);", "nothing")]
    [InlineData("text([1, \"two\", true]);", "[1, \"two\", true]")]
    public void TextExplicitlyConvertsCoreValues(string source, string expected)
    {
        Assert.Equal(new TextValue(expected), Execute(source, new List<string>()));
    }

    [Fact]
    public void TextConvertsFunctionValuesWithoutInvokingThem()
    {
        Assert.Equal(
            new TextValue("<function>"),
            Execute("function answer() { return 42; } text(answer);", new List<string>()));
    }

    [Theory]
    [InlineData("type(1);", "number")]
    [InlineData("type(\"hello\");", "text")]
    [InlineData("type(true);", "boolean")]
    [InlineData("type([1, 2]);", "list")]
    [InlineData("type([[1, 2], [3, 4]]);", "list")]
    [InlineData("type(nothing);", "nothing")]
    public void TypeReturnsPublicRuntimeTypeName(string source, string expected)
    {
        Assert.Equal(new TextValue(expected), Execute(source, new List<string>()));
    }

    [Fact]
    public void TypeReportsFunctionValuesAsFunction()
    {
        Assert.Equal(
            new TextValue("function"),
            Execute("function answer() { return 42; } type(answer);", new List<string>()));
    }

    [Fact]
    public void LexicalBindingCanShadowTypeBuiltin()
    {
        var result = Execute("let type = 17; type;", new List<string>());

        Assert.Equal(new NumberValue(17), result);
    }

    [Fact]
    public void NumberReturnsExistingNumberValue()
    {
        Assert.Equal(new NumberValue(42), Execute("number(42);", new List<string>()));
    }

    [Theory]
    [InlineData("number(\"20\");", 20d)]
    [InlineData("number(\"-3.5\");", -3.5d)]
    [InlineData("number(\"1e3\");", 1000d)]
    [InlineData("number(\"0.25\");", 0.25d)]
    public void NumberExplicitlyConvertsNumericText(string source, double expected)
    {
        Assert.Equal(new NumberValue(expected), Execute(source, new List<string>()));
    }

    [Theory]
    [InlineData("number(\"not a number\");")]
    [InlineData("number(\"NaN\");")]
    [InlineData("number(\"Infinity\");")]
    public void NumberRejectsTextThatIsNotFiniteNumericText(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Execute(source, new List<string>()));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("could not convert", error.Message);
    }

    [Theory]
    [InlineData("number(true);")]
    [InlineData("number(nothing);")]
    [InlineData("number([]);")]
    public void NumberRejectsUnrelatedRuntimeTypes(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Execute(source, new List<string>()));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("number or numeric text", error.Message);
    }

    [Fact]
    public void RangeUsesInclusiveStartAndExclusiveEnd()
    {
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(1), new NumberValue(2), new NumberValue(3), new NumberValue(4)
            }),
            Execute("range(1, 5);", new List<string>()));
    }

    [Fact]
    public void RangeSupportsNegativeBounds()
    {
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(-2), new NumberValue(-1), new NumberValue(0), new NumberValue(1)
            }),
            Execute("range(-2, 2);", new List<string>()));
    }

    [Theory]
    [InlineData("range(3, 3);")]
    [InlineData("range(5, 2);")]
    public void RangeIsEmptyWhenStartIsNotLessThanEnd(string source)
    {
        Assert.Equal(new ListValue(), Execute(source, new List<string>()));
    }

    [Theory]
    [InlineData("range(1.5, 4);")]
    [InlineData("range(1, 4.5);")]
    [InlineData("range(\"1\", 4);")]
    [InlineData("range(1, true);")]
    public void RangeRequiresWholeNumberBounds(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Execute(source, new List<string>()));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("whole-number", error.Message);
    }

    [Fact]
    public void RangeWorksAsForLoopIterable()
    {
        var output = new List<string>();

        Execute("for number in range(1, 4) { print(number); }", output);

        Assert.Equal(new[] { "1", "2", "3" }, output);
    }

    [Theory]
    [InlineData("length();")]
    [InlineData("concat([]);")]
    [InlineData("text();")]
    [InlineData("number();")]
    [InlineData("type();")]
    [InlineData("type(1, 2);")]
    [InlineData("range(1);")]
    [InlineData("range(1, 2, 3);")]
    public void EssentialBuiltinsUseStrictArity(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Execute(source, new List<string>()));

        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, error.Code);
    }

    [Fact]
    public void BuiltinSemanticFailureUsesCallSourceSpan()
    {
        const string source = "number(\"nope\");";
        var error = Assert.Throws<RuntimeError>(() => Execute(source, new List<string>()));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Equal(0, error.Span.Start.Offset);
        Assert.Equal(source.Length - 1, error.Span.End.Offset);
    }

    [Fact]
    public void UserBindingCanShadowAnyEssentialBuiltin()
    {
        var result = Execute("let range = 99; range;", new List<string>());

        Assert.Equal(new NumberValue(99), result);
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
