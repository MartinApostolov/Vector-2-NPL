using Vector.Core.Bytecode;
using Vector.Core.Bytecode.Compiler;
using Vector.Core.Bytecode.Vm;
using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Vector.Core.Syntax;
using Xunit;

namespace Vector.Tests.Bytecode;

public sealed class FunctionBytecodeTests
{
    [Fact]
    public void CompilerEmitsFunctionPrototypeClosureValidationCallAndReturn()
    {
        const string source = "function add(a, b) { return a + b; } add(2, 3);";
        var compilation = Compile(source);
        var chunk = compilation.Program.EntryPoint;

        var function = Assert.Single(chunk.Functions);
        Assert.Equal("add", function.Name);
        Assert.Equal(new[] { "a", "b" }, function.Parameters);
        Assert.Equal(2, function.Arity);
        Assert.Equal("functions-vm.vec", function.Chunk.SourceName);
        Assert.Equal(source, function.Chunk.SourceText);

        Assert.Contains(chunk.Instructions, instruction => instruction.OpCode == OpCode.MakeClosure);
        Assert.Contains(chunk.Instructions, instruction =>
            instruction.OpCode == OpCode.ValidateCall && instruction.Operand == 2);
        Assert.Contains(chunk.Instructions, instruction =>
            instruction.OpCode == OpCode.Call && instruction.Operand == 2);
        Assert.Contains(function.Chunk.Instructions, instruction => instruction.OpCode == OpCode.Return);
    }

    [Fact]
    public void FunctionCallBindsParametersAndReturnsValueLikeInterpreter()
    {
        AssertVmMatchesInterpreter(
            "function add(a, b) { return a + b; } add(5, 3);",
            new NumberValue(8));
    }

    [Fact]
    public void ParametersRemainDynamicallyTypedAndZeroArgumentCallsWork()
    {
        AssertVmMatchesInterpreter(
            "function join(a, b) { return a + b; } join(\"Vector \", \"VM\");",
            new TextValue("Vector VM"));

        AssertVmMatchesInterpreter(
            "function answer() { return 42; } answer();",
            new NumberValue(42));
    }

    [Fact]
    public void RuntimeFunctionEqualityRemainsIdentityBased()
    {
        const string source = "function first() { return 1; } function second() { return 1; } let same = first; [first == same, first == second];";

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new BooleanValue(true),
                new BooleanValue(false)
            }));
    }

    [Fact]
    public void BareReturnAndFallingOffFunctionEndReturnNothing()
    {
        AssertVmMatchesInterpreter(
            "function stop() { return; } stop();",
            NothingValue.Instance);

        AssertVmMatchesInterpreter(
            "function work() { let value = 1; value = value + 1; } work();",
            NothingValue.Instance);
    }

    [Fact]
    public void ReturnStopsRemainingBodyAndCanExitConditionalAndLoop()
    {
        const string source = """
            let touched = 0;
            function pick(flag) {
                if flag { return 10; }
                while true {
                    return 20;
                    touched = 1;
                }
                touched = 2;
            }
            [pick(true), pick(false), touched];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(10),
                new NumberValue(20),
                new NumberValue(0)
            }));
    }

    [Fact]
    public void ParametersAndTopLevelBodyDeclarationsShareInvocationScope()
    {
        AssertEquivalentRuntimeFailure(
            "function invalid(value) { let value = 2; } invalid(1);",
            DiagnosticCode.VariableAlreadyDeclared);
    }

    [Fact]
    public void WrongArityMatchesInterpreterBeforeAnyArgumentEvaluation()
    {
        AssertEquivalentRuntimeFailure(
            "function noArgs() { return 1; } noArgs(missing);",
            DiagnosticCode.ArgumentCountMismatch);

        AssertEquivalentRuntimeFailure(
            "function two(a, b) { return a + b; } two(1);",
            DiagnosticCode.ArgumentCountMismatch);
    }

    [Theory]
    [InlineData("1();")]
    [InlineData("\"text\"();")]
    [InlineData("true();")]
    [InlineData("nothing();")]
    [InlineData("[]();")]
    public void CallingNonFunctionMatchesInterpreter(string source)
    {
        AssertEquivalentRuntimeFailure(source, DiagnosticCode.RuntimeTypeError);
    }

    [Fact]
    public void ArgumentsEvaluateLeftToRightAndCalleeEvaluatesFirst()
    {
        const string source = """
            let order = 0;
            function target(value) { return value; }
            function choose() { order = order * 10 + 1; return target; }
            function first() { order = order * 10 + 2; return 10; }
            function second() { order = order * 10 + 3; return 20; }
            function add(a, b) { return a + b; }
            let chosen = choose()(add(first(), second()));
            [chosen, order];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(30),
                new NumberValue(123)
            }));
    }

    [Fact]
    public void RecursiveFunctionResolvesItsOwnBinding()
    {
        const string source = "function factorial(n) { if n <= 1 { return 1; } return n * factorial(n - 1); } factorial(6);";

        AssertVmMatchesInterpreter(source, new NumberValue(720));
    }

    [Fact]
    public void MutuallyRecursiveFunctionsCanResolveLaterExecutedDeclarations()
    {
        const string source = """
            function even(n) { if n == 0 { return true; } return odd(n - 1); }
            function odd(n) { if n == 0 { return false; } return even(n - 1); }
            even(10);
            """;

        AssertVmMatchesInterpreter(source, new BooleanValue(true));
    }

    [Fact]
    public void FunctionDeclarationsAreNotHoisted()
    {
        AssertEquivalentRuntimeFailure(
            "answer(); function answer() { return 42; }",
            DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void SameScopeFunctionConflictsMatchInterpreter()
    {
        AssertEquivalentRuntimeFailure(
            "function value() { return 1; } function value() { return 2; }",
            DiagnosticCode.VariableAlreadyDeclared);

        AssertEquivalentRuntimeFailure(
            "let value = 1; function value() { return 2; }",
            DiagnosticCode.VariableAlreadyDeclared);
    }

    [Fact]
    public void RuntimeErrorInsideFunctionBodyKeepsDiagnosticAndSourceParity()
    {
        AssertEquivalentRuntimeFailure(
            "function fail() { let local = 2; missing; } fail();",
            DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void VmUsesExplicitFramesForRecursiveExecution()
    {
        const string source = "function countdown(n) { if n == 0 { return 0; } return countdown(n - 1); } countdown(250);";
        var syntax = Parse(source);
        var compilation = new BytecodeCompiler().Compile(syntax, "functions-vm.vec", source);
        var result = new VectorVirtualMachine().Execute(compilation.Program).Result;

        Assert.Equal(new NumberValue(0), result);
    }

    private static void AssertVmMatchesInterpreter(string source, VectorValue expected)
    {
        var syntax = Parse(source);
        var interpreterResult = new Interpreter().Execute(syntax, "functions-vm.vec", source);
        var compilation = new BytecodeCompiler().Compile(syntax, "functions-vm.vec", source);
        var vmResult = new VectorVirtualMachine().Execute(compilation.Program).Result;

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static void AssertEquivalentRuntimeFailure(string source, DiagnosticCode expectedCode)
    {
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter().Execute(syntax, "functions-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "functions-vm.vec", source);
        var vmError = Assert.Throws<RuntimeError>(() =>
            new VectorVirtualMachine().Execute(compilation.Program));

        Assert.Equal(expectedCode, interpreterError.Code);
        Assert.Equal(interpreterError.Code, vmError.Code);
        Assert.Equal(interpreterError.Message, vmError.Message);
        Assert.Equal(interpreterError.Span, vmError.Span);
        Assert.Equal(interpreterError.SourceName, vmError.SourceName);
        Assert.Equal(interpreterError.SourceText, vmError.SourceText);
    }

    private static BytecodeCompilationResult Compile(string source)
    {
        var syntax = Parse(source);
        return new BytecodeCompiler().Compile(syntax, "functions-vm.vec", source);
    }

    private static CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }
}
