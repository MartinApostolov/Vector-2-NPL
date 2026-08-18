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

public sealed class ConditionalBytecodeTests
{
    [Fact]
    public void TrueIfExecutesThenBranchOnly()
    {
        const string source = "let value = 0; if true { value = 42; } else { value = -1; } value;";

        AssertVmMatchesInterpreter(source, new NumberValue(42));
    }

    [Fact]
    public void FalseIfExecutesElseBranchOnly()
    {
        const string source = "let value = 0; if false { value = -1; } else { value = 42; } value;";

        AssertVmMatchesInterpreter(source, new NumberValue(42));
    }

    [Fact]
    public void IfWithoutElseReturnsNothing()
    {
        AssertVmMatchesInterpreter("if false { 42; }", NothingValue.Instance);
        AssertVmMatchesInterpreter("if true { 42; }", NothingValue.Instance);
    }

    [Fact]
    public void ElseIfChainExecutesFirstMatchingBranch()
    {
        const string source = """
            let selector = 2;
            let value = 0;
            if selector == 1 { value = 10; }
            else if selector == 2 { value = 20; }
            else { value = 30; }
            value;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(20));
    }

    [Fact]
    public void UnselectedBranchesAndElseIfConditionsAreNotEvaluated()
    {
        const string source = """
            let value = 0;
            if true { value = 7; }
            else if missing == 1 { value = 8; }
            else { value = alsoMissing; }
            value;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(7));
    }

    [Fact]
    public void RepresentativeConditionalAndLogicalProgramMatchesInterpreter()
    {
        const string source = """
            let value = 0;
            if (true and not false) {
                value = 42;
            } else {
                value = -1;
            }
            value;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(42));
    }

    [Fact]
    public void IfConditionIsEvaluatedExactlyOnce()
    {
        const string source = """
            let flag = false;
            let count = 0;
            if (flag = true) { count = count + 1; }
            [flag, count];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new BooleanValue(true),
                new NumberValue(1)
            }));
    }

    [Fact]
    public void BranchBlocksKeepTheirLexicalScope()
    {
        const string source = "let value = 1; if true { let value = 2; value = 3; } value;";

        AssertVmMatchesInterpreter(source, new NumberValue(1));
    }

    [Theory]
    [InlineData("if 1 { 0; }")]
    [InlineData("if \"yes\" { 0; }")]
    [InlineData("if nothing { 0; }")]
    [InlineData("if [] { 0; }")]
    [InlineData("if false { 0; } else if 1 { 1; } else { 2; }")]
    public void IfAndElseIfConditionsRequireActualBooleans(string source)
    {
        AssertEquivalentRuntimeFailure(source, DiagnosticCode.RuntimeTypeError);
    }

    private static void AssertVmMatchesInterpreter(string source, VectorValue expected)
    {
        var syntax = Parse(source);
        var interpreterResult = new Interpreter().Execute(syntax, "conditionals-vm.vec", source);
        var compilation = new BytecodeCompiler().Compile(syntax, "conditionals-vm.vec", source);
        var vmResult = new VectorVirtualMachine().Execute(compilation.Program).Result;

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static void AssertEquivalentRuntimeFailure(string source, DiagnosticCode expectedCode)
    {
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter().Execute(syntax, "conditionals-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "conditionals-vm.vec", source);
        var vmError = Assert.Throws<RuntimeError>(() =>
            new VectorVirtualMachine().Execute(compilation.Program));

        Assert.Equal(expectedCode, interpreterError.Code);
        Assert.Equal(interpreterError.Code, vmError.Code);
        Assert.Equal(interpreterError.Message, vmError.Message);
        Assert.Equal(interpreterError.Span, vmError.Span);
        Assert.Equal(interpreterError.SourceName, vmError.SourceName);
        Assert.Equal(interpreterError.SourceText, vmError.SourceText);
    }

    private static CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }
}
