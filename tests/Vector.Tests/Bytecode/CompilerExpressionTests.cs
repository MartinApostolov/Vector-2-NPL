using Vector.Core.Bytecode;
using Vector.Core.Bytecode.Compiler;
using Vector.Core.Parsing;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Bytecode;

public sealed class CompilerExpressionTests
{
    [Fact]
    public void CompilerEmitsPrecedenceOrderedArithmeticBytecode()
    {
        const string source = "2 + 3 * 4;";
        var compilation = Compile(source);
        var chunk = compilation.Program.EntryPoint;

        Assert.Equal(
            new[]
            {
                OpCode.Constant,
                OpCode.Constant,
                OpCode.Constant,
                OpCode.Multiply,
                OpCode.Add,
                OpCode.Halt
            },
            chunk.Instructions.Select(instruction => instruction.OpCode));

        Assert.Equal(
            new VectorValue[]
            {
                new NumberValue(2),
                new NumberValue(3),
                new NumberValue(4)
            },
            chunk.Constants);
    }

    [Fact]
    public void CompilerPopsIntermediateExpressionStatementsAndKeepsFinalValue()
    {
        const string source = "1; 2 + 3;";
        var compilation = Compile(source);

        Assert.Equal(
            new[]
            {
                OpCode.Constant,
                OpCode.Pop,
                OpCode.Constant,
                OpCode.Constant,
                OpCode.Add,
                OpCode.Halt
            },
            compilation.Program.EntryPoint.Instructions.Select(instruction => instruction.OpCode));
    }

    [Fact]
    public void CompilerHandlesLiteralGroupingUnaryAndComparisonFamilies()
    {
        const string source = "nothing; \"a\" + \"b\"; not false; -(2); 3 >= 2; 5 != 6;";
        var compilation = Compile(source);
        var opCodes = compilation.Program.EntryPoint.Instructions
            .Select(instruction => instruction.OpCode)
            .ToArray();

        Assert.Contains(OpCode.Nothing, opCodes);
        Assert.Contains(OpCode.Add, opCodes);
        Assert.Contains(OpCode.Not, opCodes);
        Assert.Contains(OpCode.Negate, opCodes);
        Assert.Contains(OpCode.GreaterOrEqual, opCodes);
        Assert.Contains(OpCode.NotEqual, opCodes);
        Assert.Equal(OpCode.Halt, opCodes[^1]);
    }

    [Fact]
    public void CompilerPreservesSourceMetadataOnChunkAndInstructions()
    {
        const string source = "40 + 2;";
        var compilation = Compile(source, "main.vec");
        var chunk = compilation.Program.EntryPoint;

        Assert.Equal("main.vec", chunk.SourceName);
        Assert.Equal(source, chunk.SourceText);
        Assert.Equal(0, chunk.Instructions[0].Span.Start.Offset);
        Assert.Equal(2, chunk.Instructions[0].Span.End.Offset);
        Assert.Equal(0, chunk.Instructions[^2].Span.Start.Offset);
        Assert.Equal(6, chunk.Instructions[^2].Span.End.Offset);
    }

    [Fact]
    public void EmptyCompilationUnitProducesNothingThenHalt()
    {
        var compilation = Compile(string.Empty);

        Assert.Equal(
            new[] { OpCode.Nothing, OpCode.Halt },
            compilation.Program.EntryPoint.Instructions.Select(instruction => instruction.OpCode));
    }

    [Fact]
    public void CurrentCompilerStageRejectsLogicalOperatorsAndNonExpressionStatements()
    {
        Assert.Throws<NotSupportedException>(() => Compile("true and false;"));
        Assert.Throws<NotSupportedException>(() => Compile("let value = 1;"));
    }

    private static BytecodeCompilationResult Compile(string source, string? sourceName = null)
    {
        var parser = new Parser(new SourceText(source));
        var parseResult = parser.ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);

        return new BytecodeCompiler().Compile(parseResult.Root, sourceName, source);
    }
}
