using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Runtime;

public sealed class StatementRuntimeTests
{
    [Fact]
    public void CompilationUnitExecutesVariableDeclarationsAndExpressionStatements()
    {
        var environment = new RuntimeEnvironment();

        var result = Execute("let x = 10; x = x + 5; x;", environment);

        Assert.Equal(new NumberValue(15), result);
        Assert.Equal(new NumberValue(15), environment.Get("x", Span(0, 1)));
    }

    [Fact]
    public void EmptyCompilationUnitReturnsNothing()
    {
        Assert.Same(NothingValue.Instance, Execute(string.Empty));
    }

    [Fact]
    public void VariableDeclarationReturnsNothingWhenItIsLastStatement()
    {
        var environment = new RuntimeEnvironment();

        var result = Execute("let value = 20;", environment);

        Assert.Same(NothingValue.Instance, result);
        Assert.Equal(new NumberValue(20), environment.Get("value", Span(0, 5)));
    }

    [Fact]
    public void DeclarationInitializerIsEvaluatedBeforeShadowingBindingIsIntroduced()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("x", new NumberValue(10), Span(0, 1));

        environment.Declare("seen", new NumberValue(0), Span(0, 4));

        Execute("{ let x = x + 1; seen = x; }", environment);

        Assert.Equal(new NumberValue(11), environment.Get("seen", Span(0, 4)));
        Assert.Equal(new NumberValue(10), environment.Get("x", Span(0, 1)));
    }

    [Fact]
    public void SameScopeRedeclarationStillUsesEnvironmentRuntimeError()
    {
        var error = Assert.Throws<RuntimeError>(() => Execute("let x = 1; let x = 2;"));

        Assert.Equal(DiagnosticCode.VariableAlreadyDeclared, error.Code);
        Assert.Contains("'x'", error.Message);
    }

    [Fact]
    public void BlockCreatesLexicalScopeAndLocalDeclarationDoesNotLeak()
    {
        var environment = new RuntimeEnvironment();

        Execute("{ let local = 7; local; }", environment);

        var error = Assert.Throws<RuntimeError>(() => environment.Get("local", Span(0, 5)));
        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
    }

    [Fact]
    public void BlockShadowingLeavesOuterBindingUnchanged()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("x", new NumberValue(1), Span(0, 1));

        Execute("{ let x = 2; x = 3; }", environment);

        Assert.Equal(new NumberValue(1), environment.Get("x", Span(0, 1)));
    }

    [Fact]
    public void BlockAssignmentUpdatesNearestExistingOuterBindingWhenNotShadowed()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("counter", new NumberValue(0), Span(0, 7));

        Execute("{ counter = counter + 1; }", environment);

        Assert.Equal(new NumberValue(1), environment.Get("counter", Span(0, 7)));
    }

    [Fact]
    public void NestedBlocksUseNestedLexicalScopes()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("x", new NumberValue(1), Span(0, 1));

        environment.Declare("seen", new NumberValue(0), Span(0, 4));

        Execute("{ let x = 2; { let x = 3; seen = x; } }", environment);

        Assert.Equal(new NumberValue(3), environment.Get("seen", Span(0, 4)));
        Assert.Equal(new NumberValue(1), environment.Get("x", Span(0, 1)));
    }

    [Fact]
    public void BlockRestoresPreviousEnvironmentAfterRuntimeError()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("outer", new NumberValue(1), Span(0, 5));
        var interpreter = new Interpreter(environment);
        var unit = Parse("{ let local = 2; missing; }");

        Assert.Throws<RuntimeError>(() => interpreter.Execute(unit));

        Assert.Same(environment, interpreter.CurrentEnvironment);
        Assert.Equal(new NumberValue(1), environment.Get("outer", Span(0, 5)));
        Assert.Throws<RuntimeError>(() => environment.Get("local", Span(0, 5)));
    }

    [Fact]
    public void IfTrueExecutesThenBranchOnly()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("value", new NumberValue(0), Span(0, 5));

        Execute("if true { value = 1; } else { value = 2; }", environment);

        Assert.Equal(new NumberValue(1), environment.Get("value", Span(0, 5)));
    }

    [Fact]
    public void IfFalseExecutesElseBranchOnly()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("value", new NumberValue(0), Span(0, 5));

        Execute("if false { value = 1; } else { value = 2; }", environment);

        Assert.Equal(new NumberValue(2), environment.Get("value", Span(0, 5)));
    }

    [Fact]
    public void IfWithoutElseReturnsNothingWhenConditionIsFalse()
    {
        Assert.Same(NothingValue.Instance, Execute("if false { 1; }"));
    }

    [Theory]
    [InlineData(1, 20d)]
    [InlineData(2, 30d)]
    [InlineData(3, 40d)]
    public void ElseIfChainExecutesFirstMatchingBranch(int selector, double expected)
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("selector", new NumberValue(selector), Span(0, 8));
        environment.Declare("value", new NumberValue(0), Span(0, 5));

        Execute(
            "if selector == 1 { value = 20; } " +
            "else if selector == 2 { value = 30; } " +
            "else { value = 40; }",
            environment);

        Assert.Equal(new NumberValue(expected), environment.Get("value", Span(0, 5)));
    }

    [Fact]
    public void UnselectedIfBranchIsNotEvaluated()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("value", new NumberValue(0), Span(0, 5));

        Execute("if true { value = 1; } else { value = missing; }", environment);

        Assert.Equal(new NumberValue(1), environment.Get("value", Span(0, 5)));
    }

    [Fact]
    public void UnselectedElseIfConditionIsNotEvaluatedAfterEarlierMatch()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("value", new NumberValue(0), Span(0, 5));

        Execute(
            "if true { value = 1; } " +
            "else if missing == 2 { value = 2; } " +
            "else { value = 3; }",
            environment);

        Assert.Equal(new NumberValue(1), environment.Get("value", Span(0, 5)));
    }

    [Theory]
    [InlineData("if 1 { 0; }")]
    [InlineData("if \"yes\" { 0; }")]
    [InlineData("if nothing { 0; }")]
    [InlineData("if [] { 0; }")]
    public void IfConditionRequiresActualBoolean(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Execute(source));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("condition", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(error.Span.Length > 0);
    }

    [Fact]
    public void ElseIfConditionAlsoRequiresActualBoolean()
    {
        var error = Assert.Throws<RuntimeError>(() =>
            Execute("if false { 0; } else if 1 { 1; } else { 2; }"));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("condition", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IfConditionIsEvaluatedOnce()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("flag", new BooleanValue(false), Span(0, 4));
        environment.Declare("count", new NumberValue(0), Span(0, 5));

        Execute("if (flag = true) { count = count + 1; }", environment);

        Assert.Equal(new BooleanValue(true), environment.Get("flag", Span(0, 4)));
        Assert.Equal(new NumberValue(1), environment.Get("count", Span(0, 5)));
    }

    [Fact]
    public void IfBranchCreatesScopeForDeclarations()
    {
        var environment = new RuntimeEnvironment();

        Execute("if true { let branchOnly = 5; branchOnly; }", environment);

        var error = Assert.Throws<RuntimeError>(() => environment.Get("branchOnly", Span(0, 10)));
        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
    }

    [Fact]
    public void ConditionalStatementReturnsNothing()
    {
        Assert.Same(NothingValue.Instance, Execute("if true { 42; } else { 0; }"));
        Assert.Same(NothingValue.Instance, Execute("if false { 0; } else { 7; }"));
    }

    [Fact]
    public void CompilationContinuesAfterConditional()
    {
        var environment = new RuntimeEnvironment();

        var result = Execute("let x = 1; if true { x = 2; } x = x + 3; x;", environment);

        Assert.Equal(new NumberValue(5), result);
        Assert.Equal(new NumberValue(5), environment.Get("x", Span(0, 1)));
    }

    private static VectorValue Execute(string source, RuntimeEnvironment? environment = null)
    {
        var interpreter = new Interpreter(environment);
        return interpreter.Execute(Parse(source));
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
}
