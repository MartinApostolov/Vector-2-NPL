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

public sealed class ListBytecodeTests
{
    [Fact]
    public void CompilerEmitsListIndexAndIndexedAssignmentInstructions()
    {
        const string source = "let values = [1, 2, 3]; values[1] = 10; values[1];";
        var compilation = Compile(source);
        var opCodes = compilation.Program.EntryPoint.Instructions
            .Select(instruction => instruction.OpCode)
            .ToArray();

        Assert.Contains(OpCode.BuildList, opCodes);
        Assert.Contains(OpCode.RequireList, opCodes);
        Assert.Contains(OpCode.SetIndex, opCodes);
        Assert.Contains(OpCode.GetIndex, opCodes);
    }

    [Fact]
    public void CompilerValidatesIndexTargetBeforeCompilingIndexSideEffects()
    {
        const string source = "let i = 0; 5[(i = 1)];";
        var compilation = Compile(source);
        var instructions = compilation.Program.EntryPoint.Instructions;

        var requireListIndex = FindInstruction(instructions, OpCode.RequireList);
        var assignIndex = FindInstruction(instructions, OpCode.AssignVariable);

        Assert.True(requireListIndex < assignIndex);
    }

    [Fact]
    public void VmExecutesEmptyMixedAndNestedListLiteralsLikeInterpreter()
    {
        AssertVmMatchesInterpreter(
            "[[], [1, \"two\", true, nothing], [[3, 4]]];",
            new ListValue(new VectorValue[]
            {
                new ListValue(),
                new ListValue(new VectorValue[]
                {
                    new NumberValue(1),
                    new TextValue("two"),
                    new BooleanValue(true),
                    NothingValue.Instance
                }),
                new ListValue(new VectorValue[]
                {
                    new ListValue(new VectorValue[] { new NumberValue(3), new NumberValue(4) })
                })
            }));
    }

    [Fact]
    public void ListElementsEvaluateLeftToRight()
    {
        const string source = "let x = 1; let values = [(x = x + 1), (x = x * 10)]; [values, x];";

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[] { new NumberValue(2), new NumberValue(20) }),
                new NumberValue(20)
            }));
    }

    [Fact]
    public void ZeroBasedAndChainedIndexingMatchInterpreter()
    {
        AssertVmMatchesInterpreter(
            "[[1, 2], [3, 4]][1][1];",
            new NumberValue(4));
    }

    [Fact]
    public void IndexedAssignmentMutatesListAndReturnsAssignedValue()
    {
        const string source = "let values = [1, 2, 3]; let result = (values[1] = 10); [values, result];";

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[] { new NumberValue(1), new NumberValue(10), new NumberValue(3) }),
                new NumberValue(10)
            }));
    }

    [Fact]
    public void IndexedAssignmentEvaluatesRightSideBeforeTargetIndex()
    {
        const string source = "let i = 0; let items = [10, 20]; items[(i = i + 1)] = (i = 0); [items, i];";

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[] { new NumberValue(10), new NumberValue(0) }),
                new NumberValue(1)
            }));
    }

    [Theory]
    [InlineData("[1][\"0\"];", DiagnosticCode.RuntimeTypeError)]
    [InlineData("[1][-1];", DiagnosticCode.InvalidListIndex)]
    [InlineData("[1][1.5];", DiagnosticCode.InvalidListIndex)]
    [InlineData("[1][2];", DiagnosticCode.ListIndexOutOfRange)]
    [InlineData("5[0];", DiagnosticCode.RuntimeTypeError)]
    public void InvalidIndexFailuresMatchInterpreter(string source, DiagnosticCode expectedCode)
    {
        AssertEquivalentRuntimeFailure(source, expectedCode);
    }

    [Fact]
    public void IndexedAssignmentRejectsDirectSelfContainment()
    {
        AssertEquivalentRuntimeFailure(
            "let items = [nothing]; items[0] = items;",
            DiagnosticCode.CyclicList);
    }

    [Fact]
    public void IndexedAssignmentRejectsIndirectSelfContainment()
    {
        AssertEquivalentRuntimeFailure(
            "let outer = [nothing]; let inner = [outer]; outer[0] = inner;",
            DiagnosticCode.CyclicList);
    }

    private static int FindInstruction(IReadOnlyList<BytecodeInstruction> instructions, OpCode opCode)
    {
        for (var index = 0; index < instructions.Count; index++)
        {
            if (instructions[index].OpCode == opCode)
            {
                return index;
            }
        }

        throw new Xunit.Sdk.XunitException($"Expected opcode '{opCode}' was not emitted.");
    }

    private static void AssertVmMatchesInterpreter(string source, VectorValue expected)
    {
        var syntax = Parse(source);
        var interpreterResult = new Interpreter().Execute(syntax, "lists-vm.vec", source);
        var vmResult = ExecuteVm(syntax, source);

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static void AssertEquivalentRuntimeFailure(string source, DiagnosticCode expectedCode)
    {
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter().Execute(syntax, "lists-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "lists-vm.vec", source);
        var vmError = Assert.Throws<RuntimeError>(() =>
            new VectorVirtualMachine().Execute(compilation.Program));

        Assert.Equal(expectedCode, interpreterError.Code);
        Assert.Equal(interpreterError.Code, vmError.Code);
        Assert.Equal(interpreterError.Message, vmError.Message);
        Assert.Equal(interpreterError.Span, vmError.Span);
        Assert.Equal(interpreterError.SourceName, vmError.SourceName);
        Assert.Equal(interpreterError.SourceText, vmError.SourceText);
    }

    private static VectorValue ExecuteVm(CompilationUnit syntax, string source)
    {
        var compilation = new BytecodeCompiler().Compile(syntax, "lists-vm.vec", source);
        return new VectorVirtualMachine().Execute(compilation.Program).Result;
    }

    private static BytecodeCompilationResult Compile(string source)
    {
        var syntax = Parse(source);
        return new BytecodeCompiler().Compile(syntax, "lists-vm.vec", source);
    }

    private static CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }
}
