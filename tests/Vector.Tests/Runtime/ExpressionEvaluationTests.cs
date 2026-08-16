using Vector.Core.Runtime;
using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Runtime;

public sealed class ExpressionEvaluationTests
{
    public static TheoryData<string, VectorValue> Literals => new()
    {
        { "12.5", new NumberValue(12.5) },
        { "\"hello\"", new TextValue("hello") },
        { "true", new BooleanValue(true) },
        { "false", new BooleanValue(false) },
        { "nothing", NothingValue.Instance }
    };

    public static TheoryData<string, double> NumericArithmetic => new()
    {
        { "2 + 3", 5 },
        { "8 - 5", 3 },
        { "4 * 3", 12 },
        { "5 / 2", 2.5 },
        { "10 % 3", 1 },
        { "2 + 3 * 4", 14 },
        { "(2 + 3) * 4", 20 }
    };

    public static TheoryData<string, bool> NumericComparisons => new()
    {
        { "1 < 2", true },
        { "2 < 1", false },
        { "2 <= 2", true },
        { "3 > 2", true },
        { "2 > 3", false },
        { "3 >= 3", true }
    };

    public static TheoryData<string, bool> EqualityExpressions => new()
    {
        { "5 == 5", true },
        { "5 != 5", false },
        { "5 == 6", false },
        { "\"abc\" == \"abc\"", true },
        { "true != false", true },
        { "nothing == nothing", true },
        { "5 == \"5\"", false },
        { "false == nothing", false }
    };

    [Theory]
    [MemberData(nameof(Literals))]
    public void InterpreterEvaluatesLiterals(string source, VectorValue expected)
    {
        Assert.Equal(expected, Evaluate(source));
    }

    [Fact]
    public void InterpreterReadsVariablesFromLexicalEnvironment()
    {
        var environment = new RuntimeEnvironment();
        var stored = new TextValue("Vector");
        environment.Declare("name", stored, Span(0, 4));

        var result = Evaluate("name", environment);

        Assert.Same(stored, result);
    }

    [Fact]
    public void InterpreterEvaluatesGrouping()
    {
        Assert.Equal(new NumberValue(7), Evaluate("((7))"));
    }

    [Fact]
    public void InterpreterEvaluatesNameAssignmentAndReturnsAssignedValue()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("value", new TextValue("old"), Span(0, 5));

        var result = Evaluate("value = 20", environment);

        Assert.Equal(new NumberValue(20), result);
        Assert.Equal(new NumberValue(20), environment.Get("value", Span(0, 5)));
    }

    [Fact]
    public void AssignmentEvaluatesRightSideBeforeChangingBinding()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("x", new NumberValue(10), Span(0, 1));

        var result = Evaluate("x = x + 1", environment);

        Assert.Equal(new NumberValue(11), result);
        Assert.Equal(new NumberValue(11), environment.Get("x", Span(0, 1)));
    }

    [Fact]
    public void AssignmentToUndefinedVariableUsesEnvironmentRuntimeError()
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate("missing = 1"));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
        Assert.Equal(0, error.Span.Start.Offset);
        Assert.Equal(7, error.Span.End.Offset);
    }

    [Theory]
    [InlineData("-5", -5d)]
    [InlineData("--5", 5d)]
    public void InterpreterEvaluatesNumericNegation(string source, double expected)
    {
        Assert.Equal(new NumberValue(expected), Evaluate(source));
    }

    [Theory]
    [InlineData("not true", false)]
    [InlineData("not false", true)]
    [InlineData("not not true", true)]
    public void InterpreterEvaluatesBooleanNot(string source, bool expected)
    {
        Assert.Equal(new BooleanValue(expected), Evaluate(source));
    }

    [Theory]
    [MemberData(nameof(NumericArithmetic))]
    public void InterpreterEvaluatesNumericArithmetic(string source, double expected)
    {
        Assert.Equal(new NumberValue(expected), Evaluate(source));
    }

    [Fact]
    public void PlusConcatenatesTextWithText()
    {
        Assert.Equal(new TextValue("Hello Vector"), Evaluate("\"Hello \" + \"Vector\""));
    }

    [Theory]
    [MemberData(nameof(NumericComparisons))]
    public void InterpreterEvaluatesNumericComparisons(string source, bool expected)
    {
        Assert.Equal(new BooleanValue(expected), Evaluate(source));
    }

    [Theory]
    [MemberData(nameof(EqualityExpressions))]
    public void InterpreterEvaluatesEqualityWithoutCoercion(string source, bool expected)
    {
        Assert.Equal(new BooleanValue(expected), Evaluate(source));
    }

    [Fact]
    public void EqualityUsesRecursiveListValueRules()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare(
            "left",
            new ListValue(new VectorValue[]
            {
                new NumberValue(1),
                new ListValue(new VectorValue[] { new TextValue("two") })
            }),
            Span(0, 4));
        environment.Declare(
            "right",
            new ListValue(new VectorValue[]
            {
                new NumberValue(1),
                new ListValue(new VectorValue[] { new TextValue("two") })
            }),
            Span(5, 10));

        Assert.Equal(new BooleanValue(true), Evaluate("left == right", environment));
    }

    [Theory]
    [InlineData("true and true", true)]
    [InlineData("true and false", false)]
    [InlineData("false and true", false)]
    [InlineData("true or false", true)]
    [InlineData("false or true", true)]
    [InlineData("false or false", false)]
    public void InterpreterEvaluatesBooleanLogicalOperators(string source, bool expected)
    {
        Assert.Equal(new BooleanValue(expected), Evaluate(source));
    }

    [Fact]
    public void AndShortCircuitsFalseLeftOperand()
    {
        Assert.Equal(new BooleanValue(false), Evaluate("false and missing"));
    }

    [Fact]
    public void OrShortCircuitsTrueLeftOperand()
    {
        Assert.Equal(new BooleanValue(true), Evaluate("true or missing"));
    }

    [Theory]
    [InlineData("true and missing")]
    [InlineData("false or missing")]
    public void LogicalOperatorsEvaluateRightOperandWhenNeeded(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
        Assert.Contains("missing", error.Message);
    }

    [Fact]
    public void NonShortCircuitBinaryOperandsEvaluateLeftToRight()
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate("leftMissing + rightMissing"));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
        Assert.Contains("leftMissing", error.Message);
        Assert.Equal(0, error.Span.Start.Offset);
    }

    [Fact]
    public void AssignmentSideEffectsMakeLeftToRightEvaluationObservable()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("x", new NumberValue(1), Span(0, 1));

        var result = Evaluate("(x = x + 1) + (x = x * 10)", environment);

        Assert.Equal(new NumberValue(22), result);
        Assert.Equal(new NumberValue(20), environment.Get("x", Span(0, 1)));
    }

    [Theory]
    [InlineData("5 + \"2\"")]
    [InlineData("\"Age: \" + 20")]
    [InlineData("true - false")]
    [InlineData("2 * \"3\"")]
    [InlineData("\"8\" / 2")]
    [InlineData("nothing % 2")]
    [InlineData("\"a\" < \"b\"")]
    public void InvalidOperandTypesReportRuntimeTypeError(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
    }

    [Theory]
    [InlineData("not 1")]
    [InlineData("not \"true\"")]
    public void NotRequiresBooleanOperand(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("boolean", error.Message);
    }

    [Theory]
    [InlineData("1 and true")]
    [InlineData("true and 1")]
    [InlineData("\"yes\" or false")]
    [InlineData("false or nothing")]
    public void LogicalOperatorsRequireBooleanOperands(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("boolean", error.Message);
    }

    [Fact]
    public void UnaryMinusRequiresNumberOperand()
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate("-\"5\""));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("number", error.Message);
    }

    [Theory]
    [InlineData("1 / 0")]
    [InlineData("1 / -0")]
    [InlineData("1 % 0")]
    public void ZeroDivisorReportsRuntimeError(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.DivisionByZero, error.Code);
        Assert.True(error.Span.Length >= 1);
    }

    private static VectorValue Evaluate(string source, RuntimeEnvironment? environment = null)
    {
        var parser = new Parser(new SourceText(source));
        var parseResult = parser.ParseExpression();

        Assert.Empty(parseResult.Diagnostics);

        return new Interpreter(environment).Evaluate(parseResult.Root);
    }

    private static SourceSpan Span(int startOffset, int endOffset) =>
        new(
            new SourcePosition(startOffset, 1, startOffset + 1),
            new SourcePosition(endOffset, 1, endOffset + 1));
}
