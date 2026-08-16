using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Runtime;

public sealed class LoopRuntimeTests
{
    [Fact]
    public void WhileExecutesUntilConditionBecomesFalse()
    {
        var environment = new RuntimeEnvironment();

        var result = Execute("let i = 0; while i < 4 { i = i + 1; } i;", environment);

        Assert.Equal(new NumberValue(4), result);
        Assert.Equal(new NumberValue(4), environment.Get("i", Span(0, 1)));
    }

    [Fact]
    public void WhileChecksConditionBeforeFirstIteration()
    {
        var environment = new RuntimeEnvironment();

        Execute("let touched = 0; while false { touched = 1; }", environment);

        Assert.Equal(new NumberValue(0), environment.Get("touched", Span(0, 7)));
    }

    [Theory]
    [InlineData("while 1 { break; }")]
    [InlineData("while \"yes\" { break; }")]
    [InlineData("while nothing { break; }")]
    [InlineData("while [] { break; }")]
    public void WhileConditionRequiresActualBoolean(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Execute(source));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("while", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(error.Span.Length > 0);
    }

    [Fact]
    public void WhileConditionIsReevaluatedEveryIteration()
    {
        var environment = new RuntimeEnvironment();

        Execute("let i = 0; let count = 0; while (i = i + 1) < 4 { count = count + 1; }", environment);

        Assert.Equal(new NumberValue(4), environment.Get("i", Span(0, 1)));
        Assert.Equal(new NumberValue(3), environment.Get("count", Span(0, 5)));
    }

    [Fact]
    public void BreakExitsWhileBeforeRemainingBodyStatements()
    {
        var environment = new RuntimeEnvironment();

        Execute("let i = 0; let seen = 0; while true { i = i + 1; break; seen = 99; }", environment);

        Assert.Equal(new NumberValue(1), environment.Get("i", Span(0, 1)));
        Assert.Equal(new NumberValue(0), environment.Get("seen", Span(0, 4)));
    }

    [Fact]
    public void ContinueSkipsRemainingWhileBodyStatements()
    {
        var environment = new RuntimeEnvironment();

        Execute(
            "let i = 0; let sum = 0; " +
            "while i < 5 { i = i + 1; if i == 3 { continue; } sum = sum + i; }",
            environment);

        Assert.Equal(new NumberValue(12), environment.Get("sum", Span(0, 3)));
    }

    [Fact]
    public void BreakInsideIfExitsEnclosingWhile()
    {
        var environment = new RuntimeEnvironment();

        Execute("let i = 0; while true { i = i + 1; if i == 3 { break; } }", environment);

        Assert.Equal(new NumberValue(3), environment.Get("i", Span(0, 1)));
    }

    [Fact]
    public void ContinueInsideNestedBlockSkipsEnclosingWhileRemainder()
    {
        var environment = new RuntimeEnvironment();

        Execute(
            "let i = 0; let count = 0; " +
            "while i < 3 { i = i + 1; { continue; } count = count + 1; }",
            environment);

        Assert.Equal(new NumberValue(0), environment.Get("count", Span(0, 5)));
    }

    [Fact]
    public void NestedWhileBreakAffectsNearestLoopOnly()
    {
        var environment = new RuntimeEnvironment();

        Execute(
            "let outer = 0; let innerRuns = 0; " +
            "while outer < 3 { " +
            "outer = outer + 1; let inner = 0; " +
            "while inner < 5 { inner = inner + 1; innerRuns = innerRuns + 1; break; } " +
            "}",
            environment);

        Assert.Equal(new NumberValue(3), environment.Get("outer", Span(0, 5)));
        Assert.Equal(new NumberValue(3), environment.Get("innerRuns", Span(0, 9)));
    }

    [Fact]
    public void NestedLoopContinueAffectsNearestLoopOnly()
    {
        var environment = new RuntimeEnvironment();

        Execute(
            "let count = 0; " +
            "for x in [1, 2] { " +
            "for y in [1, 2, 3] { if y == 2 { continue; } count = count + 1; } " +
            "}",
            environment);

        Assert.Equal(new NumberValue(4), environment.Get("count", Span(0, 5)));
    }

    [Fact]
    public void WhileBodyGetsFreshBlockScopeEachIteration()
    {
        var environment = new RuntimeEnvironment();

        Execute(
            "let i = 0; let total = 0; " +
            "while i < 3 { let local = i; total = total + local; i = i + 1; }",
            environment);

        Assert.Equal(new NumberValue(3), environment.Get("total", Span(0, 5)));
        Assert.Throws<RuntimeError>(() => environment.Get("local", Span(0, 5)));
    }

    [Fact]
    public void ForIteratesListInOrder()
    {
        var environment = new RuntimeEnvironment();

        var result = Execute(
            "let seen = [nothing, nothing, nothing]; let i = 0; " +
            "for item in [10, 20, 30] { seen[i] = item; i = i + 1; } seen;",
            environment);

        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(10), new NumberValue(20), new NumberValue(30) }),
            result);
    }

    [Theory]
    [InlineData("for item in 1 { item; }")]
    [InlineData("for item in \"text\" { item; }")]
    [InlineData("for item in true { item; }")]
    [InlineData("for item in nothing { item; }")]
    public void ForIterableMustBeAList(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Execute(source));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("for", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForIterableExpressionIsEvaluatedOnce()
    {
        var environment = new RuntimeEnvironment();

        Execute(
            "let pick = 0; let lists = [[1], [10, 20]]; let total = 0; " +
            "for item in lists[pick = pick + 1] { total = total + item; }",
            environment);

        Assert.Equal(new NumberValue(1), environment.Get("pick", Span(0, 4)));
        Assert.Equal(new NumberValue(30), environment.Get("total", Span(0, 5)));
    }

    [Fact]
    public void ForCapturesShallowSnapshotAtLoopStart()
    {
        var environment = new RuntimeEnvironment();

        var result = Execute(
            "let values = [1, 2, 3]; let seen = [0, 0, 0]; let i = 0; " +
            "for item in values { " +
            "seen[i] = item; if i == 0 { values[1] = 99; } i = i + 1; " +
            "} seen;",
            environment);

        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(1), new NumberValue(2), new NumberValue(3) }),
            result);
        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(1), new NumberValue(99), new NumberValue(3) }),
            environment.Get("values", Span(0, 6)));
    }

    [Fact]
    public void ForLoopVariableDoesNotLeakAfterLoop()
    {
        var environment = new RuntimeEnvironment();

        Execute("for item in [1] { item; }", environment);

        var error = Assert.Throws<RuntimeError>(() => environment.Get("item", Span(0, 4)));
        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
    }

    [Fact]
    public void ForLoopVariableShadowsOuterBindingWithoutChangingIt()
    {
        var environment = new RuntimeEnvironment();

        var result = Execute("let item = 99; for item in [1, 2] { item = item + 10; } item;", environment);

        Assert.Equal(new NumberValue(99), result);
        Assert.Equal(new NumberValue(99), environment.Get("item", Span(0, 4)));
    }

    [Fact]
    public void ForUsesFreshIterationScope()
    {
        var environment = new RuntimeEnvironment();

        Execute(
            "let total = 0; " +
            "for item in [1, 2, 3] { let local = item; total = total + local; }",
            environment);

        Assert.Equal(new NumberValue(6), environment.Get("total", Span(0, 5)));
        Assert.Throws<RuntimeError>(() => environment.Get("local", Span(0, 5)));
    }

    [Fact]
    public void ForBodyCannotRedeclareLoopVariableInSameIterationScope()
    {
        var error = Assert.Throws<RuntimeError>(() => Execute("for item in [1] { let item = 2; }"));

        Assert.Equal(DiagnosticCode.VariableAlreadyDeclared, error.Code);
    }

    [Fact]
    public void ForMayIterateMixedLists()
    {
        var result = Execute(
            "let seen = [nothing, nothing, nothing, nothing]; let i = 0; " +
            "for item in [1, \"two\", true, nothing] { seen[i] = item; i = i + 1; } seen;");

        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(1),
                new TextValue("two"),
                new BooleanValue(true),
                NothingValue.Instance
            }),
            result);
    }

    [Fact]
    public void EmptyListForExecutesZeroIterations()
    {
        var environment = new RuntimeEnvironment();

        Execute("let count = 0; for item in [] { count = count + 1; }", environment);

        Assert.Equal(new NumberValue(0), environment.Get("count", Span(0, 5)));
    }

    [Fact]
    public void BreakStopsForLoopBeforeRemainingItems()
    {
        var environment = new RuntimeEnvironment();

        Execute(
            "let count = 0; for item in [1, 2, 3, 4] { " +
            "if item == 3 { break; } count = count + 1; }",
            environment);

        Assert.Equal(new NumberValue(2), environment.Get("count", Span(0, 5)));
    }

    [Fact]
    public void ContinueSkipsRemainingForBodyStatements()
    {
        var environment = new RuntimeEnvironment();

        Execute(
            "let sum = 0; for item in [1, 2, 3, 4] { " +
            "if item == 2 { continue; } sum = sum + item; }",
            environment);

        Assert.Equal(new NumberValue(8), environment.Get("sum", Span(0, 3)));
    }

    [Fact]
    public void AssigningLoopVariableDoesNotReplaceOriginalListElement()
    {
        var environment = new RuntimeEnvironment();

        var result = Execute(
            "let values = [1, 2]; for item in values { item = item * 10; } values;",
            environment);

        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(1), new NumberValue(2) }),
            result);
    }

    [Fact]
    public void WhileStatementReturnsNothingWhenLastStatement()
    {
        Assert.Same(NothingValue.Instance, Execute("let i = 0; while i < 1 { i = i + 1; }"));
    }

    [Fact]
    public void ForStatementReturnsNothingWhenLastStatement()
    {
        Assert.Same(NothingValue.Instance, Execute("for item in [1, 2] { item; }"));
    }

    [Fact]
    public void CompilationContinuesAfterWhileLoop()
    {
        Assert.Equal(
            new NumberValue(5),
            Execute("let i = 0; while i < 3 { i = i + 1; } i = i + 2; i;"));
    }

    [Fact]
    public void CompilationContinuesAfterForLoop()
    {
        Assert.Equal(
            new NumberValue(7),
            Execute("let total = 0; for item in [2, 5] { total = total + item; } total;"));
    }

    [Fact]
    public void BreakRestoresEnvironmentBeforeLeavingLoop()
    {
        var environment = new RuntimeEnvironment();
        var interpreter = new Interpreter(environment);

        interpreter.Execute(Parse("while true { let local = 1; break; }"));

        Assert.Same(environment, interpreter.CurrentEnvironment);
        Assert.Throws<RuntimeError>(() => environment.Get("local", Span(0, 5)));
    }

    [Fact]
    public void ContinueRestoresEnvironmentBeforeNextIteration()
    {
        var environment = new RuntimeEnvironment();
        var interpreter = new Interpreter(environment);

        interpreter.Execute(Parse("let i = 0; while i < 2 { let local = i; i = i + 1; continue; }"));

        Assert.Same(environment, interpreter.CurrentEnvironment);
        Assert.Throws<RuntimeError>(() => environment.Get("local", Span(0, 5)));
        Assert.Equal(new NumberValue(2), environment.Get("i", Span(0, 1)));
    }

    [Fact]
    public void ParserReportsBreakOutsideLoop()
    {
        var result = ParseWithDiagnostics("break;");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidLoopControl, diagnostic.Code);
        Assert.Contains("break", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParserReportsContinueOutsideLoop()
    {
        var result = ParseWithDiagnostics("continue;");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidLoopControl, diagnostic.Code);
        Assert.Contains("continue", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static VectorValue Execute(string source, RuntimeEnvironment? environment = null)
    {
        var interpreter = new Interpreter(environment);
        return interpreter.Execute(Parse(source));
    }

    private static Vector.Core.Syntax.CompilationUnit Parse(string source)
    {
        var result = ParseWithDiagnostics(source);
        Assert.Empty(result.Diagnostics);
        return result.Root;
    }

    private static ParseResult<Vector.Core.Syntax.CompilationUnit> ParseWithDiagnostics(string source)
    {
        var parser = new Parser(new SourceText(source));
        return parser.ParseCompilationUnit();
    }

    private static SourceSpan Span(int startOffset, int endOffset) =>
        new(
            new SourcePosition(startOffset, 1, startOffset + 1),
            new SourcePosition(endOffset, 1, endOffset + 1));
}
