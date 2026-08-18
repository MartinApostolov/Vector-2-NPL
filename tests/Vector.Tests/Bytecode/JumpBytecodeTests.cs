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

public sealed class JumpBytecodeTests
{
    [Fact]
    public void CompilerPatchesConditionalAndUnconditionalJumpTargets()
    {
        const string source = "if true { 1; } else { 2; }";
        var compilation = Compile(source);
        var instructions = compilation.Program.EntryPoint.Instructions;
        var jumps = instructions
            .Where(instruction => instruction.OpCode is OpCode.Jump or OpCode.JumpIfFalse or OpCode.JumpIfTrue)
            .ToArray();

        Assert.Contains(jumps, instruction => instruction.OpCode == OpCode.JumpIfFalse);
        Assert.Contains(jumps, instruction => instruction.OpCode == OpCode.Jump);

        foreach (var jump in jumps)
        {
            var target = Assert.IsType<int>(jump.Operand);
            Assert.InRange(target, 0, instructions.Count);
        }
    }

    [Fact]
    public void CompilerUsesOppositeConditionalJumpsForAndAndOr()
    {
        const string source = "true and false; false or true;";
        var opCodes = Compile(source).Program.EntryPoint.Instructions
            .Select(instruction => instruction.OpCode)
            .ToArray();

        Assert.Contains(OpCode.JumpIfFalse, opCodes);
        Assert.Contains(OpCode.JumpIfTrue, opCodes);
        Assert.Contains(OpCode.RequireBoolean, opCodes);
    }

    [Fact]
    public void AndAndOrShortCircuitUndefinedRightOperands()
    {
        const string source = "let a = false and missing; let b = true or missing; [a, b];";

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new BooleanValue(false),
                new BooleanValue(true)
            }));
    }

    [Fact]
    public void ShortCircuitSkipsRightSideEffects()
    {
        const string source = """
            let value = 0;
            false and ((value = 1) == 1);
            true or ((value = 2) == 2);
            value;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(0));
    }

    [Fact]
    public void NestedLogicalExpressionsPreserveShortCircuitBehavior()
    {
        const string source = "(true and false) and missing or true;";

        AssertVmMatchesInterpreter(source, new BooleanValue(true));
    }

    [Theory]
    [InlineData("1 and true;")]
    [InlineData("true and 1;")]
    [InlineData("1 or false;")]
    [InlineData("false or 1;")]
    public void EvaluatedLogicalOperandsRequireActualBooleans(string source)
    {
        AssertEquivalentRuntimeFailure(source, DiagnosticCode.RuntimeTypeError);
    }

    [Theory]
    [InlineData("false and 1;", false)]
    [InlineData("true or 1;", true)]
    public void ShortCircuitedNonBooleanRightOperandIsNotValidated(string source, bool expected)
    {
        AssertVmMatchesInterpreter(source, new BooleanValue(expected));
    }

    private static void AssertVmMatchesInterpreter(string source, VectorValue expected)
    {
        var syntax = Parse(source);
        var interpreterResult = new Interpreter().Execute(syntax, "jumps-vm.vec", source);
        var compilation = new BytecodeCompiler().Compile(syntax, "jumps-vm.vec", source);
        var vmResult = new VectorVirtualMachine().Execute(compilation.Program).Result;

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static void AssertEquivalentRuntimeFailure(string source, DiagnosticCode expectedCode)
    {
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter().Execute(syntax, "jumps-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "jumps-vm.vec", source);
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
        return new BytecodeCompiler().Compile(syntax, "jumps-vm.vec", source);
    }

    private static CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }
}
