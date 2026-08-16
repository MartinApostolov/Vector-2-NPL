using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Callable;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Runtime;

public sealed class FunctionRuntimeTests
{
    [Fact]
    public void FunctionDeclarationCreatesRuntimeFunctionValue()
    {
        var environment = new RuntimeEnvironment();

        var result = Execute("function add(a, b) { return a + b; }", environment);

        Assert.Same(NothingValue.Instance, result);
        var function = Assert.IsType<UserFunction>(environment.Get("add", Span(0, 3)));
        Assert.Equal("add", function.Name);
        Assert.Equal(2, function.Arity);
    }

    [Fact]
    public void FunctionCallBindsParametersAndReturnsValue()
    {
        Assert.Equal(
            new NumberValue(8),
            Execute("function add(a, b) { return a + b; } add(5, 3);"));
    }

    [Fact]
    public void ParametersAreDynamicallyTyped()
    {
        Assert.Equal(
            new TextValue("Vector language"),
            Execute("function join(a, b) { return a + b; } join(\"Vector \", \"language\");"));
    }

    [Fact]
    public void ZeroArgumentFunctionCanBeCalled()
    {
        Assert.Equal(
            new NumberValue(42),
            Execute("function answer() { return 42; } answer();"));
    }

    [Fact]
    public void TooFewArgumentsAreRuntimeError()
    {
        var error = Assert.Throws<RuntimeError>(() =>
            Execute("function add(a, b) { return a + b; } add(1);"));

        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, error.Code);
        Assert.Contains("2", error.Message);
        Assert.Contains("1", error.Message);
    }

    [Fact]
    public void TooManyArgumentsAreRuntimeError()
    {
        var error = Assert.Throws<RuntimeError>(() =>
            Execute("function identity(value) { return value; } identity(1, 2);"));

        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, error.Code);
        Assert.Contains("1", error.Message);
        Assert.Contains("2", error.Message);
    }

    [Fact]
    public void InvalidArityIsRejectedBeforeArgumentEvaluation()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("touched", new NumberValue(0), Span(0, 7));

        Assert.Throws<RuntimeError>(() =>
            Execute("function noArgs() { return 1; } noArgs(touched = 1);", environment));

        Assert.Equal(new NumberValue(0), environment.Get("touched", Span(0, 7)));
    }

    [Theory]
    [InlineData("1()")]
    [InlineData("\"text\"()")]
    [InlineData("true()")]
    [InlineData("nothing()")]
    [InlineData("[]()")]
    public void CallingNonFunctionIsRuntimeTypeError(string expression)
    {
        var error = Assert.Throws<RuntimeError>(() => Execute(expression + ";"));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("function", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FunctionsCanBeStoredInVariablesAndCalled()
    {
        Assert.Equal(
            new NumberValue(5),
            Execute(
                "function add(a, b) { return a + b; } " +
                "let operation = add; operation(2, 3);"));
    }

    [Fact]
    public void RuntimeFunctionsCompareByIdentity()
    {
        var result = Execute(
            "function first() { return 1; } " +
            "function second() { return 1; } " +
            "let same = first; [first == same, first == second];");

        Assert.Equal(
            new ListValue(new VectorValue[] { new BooleanValue(true), new BooleanValue(false) }),
            result);
    }

    [Fact]
    public void ReachingEndOfFunctionReturnsNothing()
    {
        Assert.Same(
            NothingValue.Instance,
            Execute("function doWork() { let x = 1; x = x + 1; } doWork();"));
    }

    [Fact]
    public void BareReturnReturnsNothing()
    {
        Assert.Same(
            NothingValue.Instance,
            Execute("function stop() { return; } stop();"));
    }

    [Fact]
    public void ReturnStopsRemainingFunctionBody()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("touched", new NumberValue(0), Span(0, 7));

        var result = Execute(
            "function pick() { return 5; touched = 1; } pick();",
            environment);

        Assert.Equal(new NumberValue(5), result);
        Assert.Equal(new NumberValue(0), environment.Get("touched", Span(0, 7)));
    }

    [Fact]
    public void ReturnInsideConditionalExitsFunction()
    {
        Assert.Equal(
            new NumberValue(10),
            Execute("function choose(flag) { if flag { return 10; } return 20; } choose(true);"));
    }

    [Fact]
    public void ReturnInsideLoopExitsFunction()
    {
        Assert.Equal(
            new NumberValue(3),
            Execute(
                "function findThree() { let i = 0; while true { i = i + 1; if i == 3 { return i; } } } " +
                "findThree();"));
    }

    [Fact]
    public void FunctionLocalDeclarationsDoNotLeak()
    {
        var environment = new RuntimeEnvironment();

        Execute("function work() { let local = 7; } work();", environment);

        Assert.Throws<RuntimeError>(() => environment.Get("local", Span(0, 5)));
    }

    [Fact]
    public void ParametersDoNotLeakAfterCall()
    {
        var environment = new RuntimeEnvironment();

        Execute("function identity(value) { return value; } identity(7);", environment);

        Assert.Throws<RuntimeError>(() => environment.Get("value", Span(0, 5)));
    }

    [Fact]
    public void ParametersAndTopLevelFunctionBodyDeclarationsShareScope()
    {
        var error = Assert.Throws<RuntimeError>(() =>
            Execute("function invalid(value) { let value = 2; } invalid(1);"));

        Assert.Equal(DiagnosticCode.VariableAlreadyDeclared, error.Code);
    }

    [Fact]
    public void FunctionReadsCapturedOuterBinding()
    {
        Assert.Equal(
            new NumberValue(15),
            Execute("let base = 10; function addBase(value) { return base + value; } addBase(5);"));
    }

    [Fact]
    public void FunctionAssignmentUpdatesCapturedOuterBinding()
    {
        var environment = new RuntimeEnvironment();

        Execute(
            "let counter = 0; function increase() { counter = counter + 1; } increase(); increase();",
            environment);

        Assert.Equal(new NumberValue(2), environment.Get("counter", Span(0, 7)));
    }

    [Fact]
    public void FunctionLocalShadowingLeavesOuterBindingUnchanged()
    {
        var environment = new RuntimeEnvironment();

        var result = Execute(
            "let value = 10; function change() { let value = 20; value = 30; return value; } " +
            "let inside = change(); [inside, value];",
            environment);

        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(30), new NumberValue(10) }),
            result);
    }

    [Fact]
    public void ClosureKeepsBlockEnvironmentAliveAfterBlockEnds()
    {
        Assert.Equal(
            new NumberValue(17),
            Execute(
                "let saved = nothing; " +
                "{ let captured = 12; function addCaptured(x) { return captured + x; } saved = addCaptured; } " +
                "saved(5);"));
    }

    [Fact]
    public void SeparateFunctionCallsProduceSeparateCapturedEnvironments()
    {
        var result = Execute(
            "function makeAdder(base) { function add(value) { return base + value; } return add; } " +
            "let add2 = makeAdder(2); let add10 = makeAdder(10); [add2(5), add10(5)];");

        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(7), new NumberValue(15) }),
            result);
    }

    [Fact]
    public void ClosureCanMutateCapturedFunctionLocalAcrossCalls()
    {
        Assert.Equal(
            new NumberValue(2),
            Execute(
                "function makeCounter() { " +
                "let count = 0; function next() { count = count + 1; return count; } return next; } " +
                "let counter = makeCounter(); counter(); counter();"));
    }

    [Fact]
    public void RecursiveFunctionCanResolveItsOwnName()
    {
        Assert.Equal(
            new NumberValue(120),
            Execute(
                "function factorial(n) { if n <= 1 { return 1; } return n * factorial(n - 1); } " +
                "factorial(5);"));
    }

    [Fact]
    public void FunctionsCanReferenceLaterDeclarationsWhenCalledAfterThoseDeclarationsExecute()
    {
        Assert.Equal(
            new BooleanValue(true),
            Execute(
                "function even(n) { if n == 0 { return true; } return odd(n - 1); } " +
                "function odd(n) { if n == 0 { return false; } return even(n - 1); } " +
                "even(6);"));
    }

    [Fact]
    public void FunctionDeclarationsAreNotHoisted()
    {
        var error = Assert.Throws<RuntimeError>(() =>
            Execute("answer(); function answer() { return 42; }"));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
        Assert.Contains("answer", error.Message);
    }

    [Fact]
    public void NestedFunctionDoesNotLeakUnlessReturnedOrAssignedOutward()
    {
        var environment = new RuntimeEnvironment();

        Execute("function outer() { function inner() { return 1; } inner(); } outer();", environment);

        Assert.Throws<RuntimeError>(() => environment.Get("inner", Span(0, 5)));
    }

    [Fact]
    public void SameScopeFunctionRedeclarationIsError()
    {
        var error = Assert.Throws<RuntimeError>(() =>
            Execute("function value() { return 1; } function value() { return 2; }"));

        Assert.Equal(DiagnosticCode.VariableAlreadyDeclared, error.Code);
    }

    [Fact]
    public void FunctionDeclarationConflictsWithExistingSameScopeVariable()
    {
        var error = Assert.Throws<RuntimeError>(() =>
            Execute("let value = 1; function value() { return 2; }"));

        Assert.Equal(DiagnosticCode.VariableAlreadyDeclared, error.Code);
    }

    [Fact]
    public void ArgumentsEvaluateLeftToRight()
    {
        var result = Execute(
            "let order = 0; " +
            "function first() { order = order * 10 + 1; return 10; } " +
            "function second() { order = order * 10 + 2; return 20; } " +
            "function add(a, b) { return a + b; } " +
            "let sum = add(first(), second()); [sum, order];");

        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(30), new NumberValue(12) }),
            result);
    }

    [Fact]
    public void CalleeEvaluatesBeforeArguments()
    {
        var result = Execute(
            "let order = 0; " +
            "function target(value) { return value; } " +
            "function choose() { order = order * 10 + 1; return target; } " +
            "function argument() { order = order * 10 + 2; return 5; } " +
            "let value = choose()(argument()); [value, order];");

        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(5), new NumberValue(12) }),
            result);
    }

    [Fact]
    public void EarlierArgumentSideEffectsRemainWhenLaterArgumentFails()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("order", new NumberValue(0), Span(0, 5));

        Assert.Throws<RuntimeError>(() => Execute(
            "function first() { order = 1; return 10; } " +
            "function add(a, b) { return a + b; } " +
            "add(first(), missing);",
            environment));

        Assert.Equal(new NumberValue(1), environment.Get("order", Span(0, 5)));
    }

    [Fact]
    public void RuntimeErrorInsideFunctionRestoresCallerEnvironment()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("outer", new NumberValue(1), Span(0, 5));
        var interpreter = new Interpreter(environment);
        var unit = Parse("function fail() { let local = 2; missing; } fail();");

        Assert.Throws<RuntimeError>(() => interpreter.Execute(unit));

        Assert.Same(environment, interpreter.CurrentEnvironment);
        Assert.Equal(new NumberValue(1), environment.Get("outer", Span(0, 5)));
        Assert.Throws<RuntimeError>(() => environment.Get("local", Span(0, 5)));
    }

    [Fact]
    public void ReturnValueMayItselfBeAFunction()
    {
        var value = Execute(
            "function source() { function returned() { return 9; } return returned; } source();");

        Assert.IsType<UserFunction>(value);
    }

    [Fact]
    public void FunctionMayAssignOuterVariableToDifferentRuntimeType()
    {
        var environment = new RuntimeEnvironment();

        Execute("let value = 1; function change() { value = \"text\"; } change();", environment);

        Assert.Equal(new TextValue("text"), environment.Get("value", Span(0, 5)));
    }

    [Fact]
    public void ParserStillReportsReturnOutsideFunction()
    {
        var result = ParseWithDiagnostics("return 1;");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidReturn, diagnostic.Code);
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
