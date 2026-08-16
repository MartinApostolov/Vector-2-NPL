using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Runtime;

public sealed class NativeFunctionTests
{
    [Fact]
    public void NativeFunctionExposesNameAndFixedArity()
    {
        var function = new NativeFunction("double", 1, (_, arguments) => arguments[0]);

        Assert.Equal("double", function.Name);
        Assert.Equal(1, function.Arity);
    }

    [Fact]
    public void OneArgumentNativeFunctionParticipatesInNormalVectorCalls()
    {
        var result = ExecuteWithNative(
            "square(5);",
            new NativeFunction(
                "square",
                1,
                (_, arguments) =>
                {
                    var value = NativeValueConverter.ToNumber(arguments[0], "value");
                    return NativeValueConverter.FromNumber(value * value);
                }));

        Assert.Equal(new NumberValue(25), result);
    }

    [Fact]
    public void TwoArgumentNativeFunctionParticipatesInNormalVectorCalls()
    {
        var result = ExecuteWithNative(
            "max2(3, 7);",
            new NativeFunction(
                "max2",
                2,
                (_, arguments) => NativeValueConverter.FromNumber(
                    Math.Max(
                        NativeValueConverter.ToNumber(arguments[0], "left"),
                        NativeValueConverter.ToNumber(arguments[1], "right")))));

        Assert.Equal(new NumberValue(7), result);
    }

    [Fact]
    public void NativeFunctionCanReturnAnyValidVectorValue()
    {
        var result = ExecuteWithNative(
            "answer();",
            new NativeFunction("answer", 0, (_, _) => new TextValue("ready")));

        Assert.Equal(new TextValue("ready"), result);
    }

    [Fact]
    public void WrongNativeArityIsRejectedBeforeArgumentsExecute()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("touched", new NumberValue(0), Span());
        environment.Declare(
            "identity",
            new NativeFunction("identity", 1, (_, arguments) => arguments[0]),
            Span());
        var interpreter = new Interpreter(environment);

        var error = Assert.Throws<RuntimeError>(() =>
            interpreter.Execute(Parse("identity(touched = 1, touched = 2);")));

        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, error.Code);
        Assert.Equal(new NumberValue(0), environment.Get("touched", Span()));
    }

    [Fact]
    public void WrongNativeArgumentTypeBecomesStructuredVectorTypeError()
    {
        var error = Assert.Throws<RuntimeError>(() =>
            ExecuteWithNative(
                "double(\"5\");",
                new NativeFunction(
                    "double",
                    1,
                    (_, arguments) => NativeValueConverter.FromNumber(
                        NativeValueConverter.ToNumber(arguments[0], "value") * 2))));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("value", error.Message);
        Assert.Contains("text", error.Message);
    }

    [Fact]
    public void DeliberateNativeFailureUsesVectorCallSiteSpan()
    {
        const string source = "nativeFail(1);";
        var error = Assert.Throws<RuntimeError>(() =>
            ExecuteWithNative(
                source,
                new NativeFunction(
                    "nativeFail",
                    1,
                    (_, _) => throw new NativeRuntimeException(
                        DiagnosticCode.RuntimeTypeError,
                        "Deliberate native failure."))));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Equal("Deliberate native failure.", error.Message);
        Assert.Equal(0, error.Span.Start.Offset);
        Assert.Equal(source.Length - 1, error.Span.End.Offset);
    }

    [Fact]
    public void UnexpectedHostExceptionBecomesSafeVectorRuntimeError()
    {
        var error = Assert.Throws<RuntimeError>(() =>
            ExecuteWithNative(
                "explode();",
                new NativeFunction(
                    "explode",
                    0,
                    (_, _) => throw new InvalidOperationException("host implementation secret"))));

        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, error.Code);
        Assert.Equal("Native function 'explode' failed.", error.Message);
        Assert.DoesNotContain("secret", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", error.Message);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteNativeNumericReturnIsRejected(double value)
    {
        var error = Assert.Throws<RuntimeError>(() =>
            ExecuteWithNative(
                "badNumber();",
                new NativeFunction("badNumber", 0, (_, _) => new NumberValue(value))));

        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, error.Code);
        Assert.Contains("non-finite", error.Message);
    }

    [Fact]
    public void NonFiniteNumberNestedInNativeListReturnIsRejected()
    {
        var error = Assert.Throws<RuntimeError>(() =>
            ExecuteWithNative(
                "badList();",
                new NativeFunction(
                    "badList",
                    0,
                    (_, _) => new ListValue(new VectorValue[]
                    {
                        new NumberValue(1),
                        new ListValue(new VectorValue[] { new NumberValue(double.NaN) })
                    }))));

        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, error.Code);
    }

    private static VectorValue ExecuteWithNative(string source, NativeFunction function)
    {
        var environment = new RuntimeEnvironment();
        environment.Declare(function.Name, function, Span());
        return new Interpreter(environment).Execute(Parse(source));
    }

    private static Vector.Core.Syntax.CompilationUnit Parse(string source)
    {
        var parser = new Parser(new SourceText(source));
        var parseResult = parser.ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }

    private static SourceSpan Span() =>
        new(new SourcePosition(0, 1, 1), new SourcePosition(0, 1, 1));
}
