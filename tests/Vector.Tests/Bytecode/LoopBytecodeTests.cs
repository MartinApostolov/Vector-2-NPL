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

public sealed class LoopBytecodeTests
{
    [Fact]
    public void CompilerEmitsBackwardJumpForWhileLoop()
    {
        const string source = "let i = 0; while i < 2 { i = i + 1; } i;";
        var instructions = Compile(source).Program.EntryPoint.Instructions;

        var hasBackwardJump = false;
        for (var index = 0; index < instructions.Count; index++)
        {
            var instruction = instructions[index];
            if (instruction.OpCode == OpCode.Jump
                && instruction.Operand is int target
                && target < index)
            {
                hasBackwardJump = true;
                break;
            }
        }

        Assert.True(hasBackwardJump);
        Assert.Contains(instructions, instruction => instruction.OpCode == OpCode.JumpIfFalse);
        Assert.Contains(instructions, instruction => instruction.OpCode == OpCode.RequireBoolean);
    }

    [Fact]
    public void WhileLoopMatchesInterpreterAndReevaluatesCondition()
    {
        const string source = "let i = 0; let total = 0; while (i = i + 1) < 5 { total = total + i; } [i, total];";

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(5),
                new NumberValue(10)
            }));
    }

    [Fact]
    public void WhileFalseExecutesZeroIterationsAndReturnsNothing()
    {
        AssertVmMatchesInterpreter("while false { 42; }", NothingValue.Instance);
    }

    [Theory]
    [InlineData("while 1 { break; }")]
    [InlineData("while \"yes\" { break; }")]
    [InlineData("while nothing { break; }")]
    [InlineData("while [] { break; }")]
    public void WhileConditionRequiresActualBoolean(string source)
    {
        AssertEquivalentRuntimeFailure(source, DiagnosticCode.RuntimeTypeError);
    }

    [Fact]
    public void ContinueInsideNestedBlockUnwindsScopesBeforeNextWhileIteration()
    {
        const string source = """
            let i = 0;
            let total = 0;
            while i < 4 {
                i = i + 1;
                {
                    let local = i;
                    if local == 2 { continue; }
                }
                total = total + i;
            }
            total;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(8));
    }

    [Fact]
    public void BreakInsideNestedBlockUnwindsScopesAndExitsWhile()
    {
        const string source = """
            let i = 0;
            while true {
                i = i + 1;
                {
                    let local = i;
                    if local == 3 { break; }
                }
            }
            i;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(3));
    }

    [Fact]
    public void NestedWhileBreakAndContinueAffectNearestLoopOnly()
    {
        const string source = """
            let outer = 0;
            let count = 0;
            while outer < 3 {
                outer = outer + 1;
                let inner = 0;
                while inner < 4 {
                    inner = inner + 1;
                    if inner == 2 { continue; }
                    count = count + 1;
                    if inner == 3 { break; }
                }
            }
            count;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(6));
    }

    [Fact]
    public void CompilerEmitsForSnapshotAndCountInstructions()
    {
        const string source = "for item in [1, 2] { item; }";
        var instructions = Compile(source).Program.EntryPoint.Instructions;
        var opCodes = instructions.Select(instruction => instruction.OpCode).ToArray();

        Assert.Contains(OpCode.RequireList, opCodes);
        Assert.Contains(OpCode.SnapshotList, opCodes);
        Assert.Contains(OpCode.ListCount, opCodes);
        Assert.Contains(OpCode.GetIndex, opCodes);
        Assert.Contains(OpCode.JumpIfFalse, opCodes);
    }

    [Fact]
    public void RepresentativeForBreakContinueProgramReturnsSeven()
    {
        const string source = """
            let total = 0;

            for value in [1, 2, 3, 4, 5] {
                if (value == 3) { continue; }
                if (value == 5) { break; }
                total = total + value;
            }

            total;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(7));
    }

    [Fact]
    public void ForIterableExpressionIsEvaluatedExactlyOnce()
    {
        const string source = """
            let pick = 0;
            let lists = [[1], [10, 20]];
            let total = 0;
            for item in lists[pick = pick + 1] {
                total = total + item;
            }
            [pick, total];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(1),
                new NumberValue(30)
            }));
    }

    [Fact]
    public void ForUsesShallowSnapshotWhenOriginalListIsMutated()
    {
        const string source = """
            let values = [1, 2, 3];
            let seen = [0, 0, 0];
            let i = 0;
            for item in values {
                seen[i] = item;
                if i == 0 { values[1] = 99; }
                i = i + 1;
            }
            [seen, values];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[]
                {
                    new NumberValue(1),
                    new NumberValue(2),
                    new NumberValue(3)
                }),
                new ListValue(new VectorValue[]
                {
                    new NumberValue(1),
                    new NumberValue(99),
                    new NumberValue(3)
                })
            }));
    }

    [Fact]
    public void ForLoopVariableSharesFreshIterationScopeWithBody()
    {
        AssertEquivalentRuntimeFailure(
            "for item in [1] { let item = 2; }",
            DiagnosticCode.VariableAlreadyDeclared);
    }

    [Fact]
    public void ForLoopVariableDoesNotLeakAndOuterShadowIsPreserved()
    {
        const string source = "let item = 99; for item in [1, 2] { item = item * 10; } item;";

        AssertVmMatchesInterpreter(source, new NumberValue(99));
    }

    [Fact]
    public void ForBodyGetsFreshScopeEachIteration()
    {
        const string source = "let total = 0; for item in [1, 2, 3] { let local = item; total = total + local; } total;";

        AssertVmMatchesInterpreter(source, new NumberValue(6));
    }

    [Fact]
    public void EmptyForLoopExecutesZeroIterationsAndReturnsNothing()
    {
        AssertVmMatchesInterpreter("for item in [] { item; }", NothingValue.Instance);
    }

    [Theory]
    [InlineData("for item in 1 { item; }")]
    [InlineData("for item in \"text\" { item; }")]
    [InlineData("for item in true { item; }")]
    [InlineData("for item in nothing { item; }")]
    public void ForIterableMustBeList(string source)
    {
        AssertEquivalentRuntimeFailure(source, DiagnosticCode.RuntimeTypeError);
    }

    [Fact]
    public void NestedForContinueAndBreakAffectNearestLoopOnly()
    {
        const string source = """
            let count = 0;
            for x in [1, 2] {
                for y in [1, 2, 3, 4] {
                    if y == 2 { continue; }
                    count = count + 1;
                    if y == 3 { break; }
                }
            }
            count;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(4));
    }

    [Fact]
    public void ContinueInForUnwindsNestedScopesBeforeAdvancingIndex()
    {
        const string source = """
            let total = 0;
            for item in [1, 2, 3] {
                {
                    let local = item;
                    if local == 2 { continue; }
                }
                total = total + item;
            }
            total;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(4));
    }

    [Fact]
    public void WhileAndForCanBeNestedTogether()
    {
        const string source = """
            let outer = 0;
            let total = 0;
            while outer < 2 {
                outer = outer + 1;
                for item in [1, 2, 3] {
                    if item == 2 { continue; }
                    total = total + item;
                }
            }
            total;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(8));
    }

    private static void AssertVmMatchesInterpreter(string source, VectorValue expected)
    {
        var syntax = Parse(source);
        var interpreterResult = new Interpreter().Execute(syntax, "loops-vm.vec", source);
        var compilation = new BytecodeCompiler().Compile(syntax, "loops-vm.vec", source);
        var vmResult = new VectorVirtualMachine().Execute(compilation.Program).Result;

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static void AssertEquivalentRuntimeFailure(string source, DiagnosticCode expectedCode)
    {
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter().Execute(syntax, "loops-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "loops-vm.vec", source);
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
        return new BytecodeCompiler().Compile(syntax, "loops-vm.vec", source);
    }

    private static CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }
}
