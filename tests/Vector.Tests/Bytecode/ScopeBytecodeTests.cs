using Vector.Core.Bytecode;
using Vector.Core.Bytecode.Compiler;
using Vector.Core.Bytecode.Vm;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Vector.Core.Syntax;
using Xunit;

namespace Vector.Tests.Bytecode;

public sealed class ScopeBytecodeTests
{
    [Fact]
    public void CompilerEmitsExplicitScopeInstructionsAndBlockNothingResult()
    {
        const string source = "let value = 1; { let inner = value; inner; } value;";
        var compilation = Compile(source);
        var opCodes = compilation.Program.EntryPoint.Instructions
            .Select(instruction => instruction.OpCode)
            .ToArray();

        Assert.Contains(OpCode.EnterScope, opCodes);
        Assert.Contains(OpCode.ExitScope, opCodes);

        var exitIndex = Array.IndexOf(opCodes, OpCode.ExitScope);
        Assert.True(exitIndex >= 0);
        Assert.Equal(OpCode.Nothing, opCodes[exitIndex + 1]);
    }

    [Fact]
    public void BlockStatementEvaluatesToNothing()
    {
        AssertVmMatchesInterpreter("{ 42; }", NothingValue.Instance);
    }

    [Fact]
    public void NestedBindingShadowsWithoutLeakingIntoOuterScope()
    {
        const string source = "let value = 1; { let value = 2; value = 3; } value;";

        AssertVmMatchesInterpreter(source, new NumberValue(1));
    }

    [Fact]
    public void AssignmentInsideBlockUpdatesNearestEnclosingBinding()
    {
        const string source = "let value = 10; { let inner = value + 5; value = inner; } value;";

        AssertVmMatchesInterpreter(source, new NumberValue(15));
    }

    [Fact]
    public void NestedAssignmentTargetsNearestShadowedBinding()
    {
        const string source = """
            let result = 0;
            {
                let value = 2;
                {
                    value = 3;
                }
                result = value;
            }
            result;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(3));
    }

    [Fact]
    public void SameNameMayBeDeclaredInNestedScope()
    {
        const string source = "let value = 1; { let value = 2; } value;";

        AssertVmMatchesInterpreter(source, new NumberValue(1));
    }

    [Fact]
    public void BlockLocalIsUndefinedAfterScopeExit()
    {
        const string source = "{ let local = 42; } local;";
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter().Execute(syntax, "scope.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "scope.vec", source);
        var vmError = Assert.Throws<RuntimeError>(() =>
            new VectorVirtualMachine().Execute(compilation.Program));

        Assert.Equal(interpreterError.Code, vmError.Code);
        Assert.Equal(interpreterError.Message, vmError.Message);
        Assert.Equal(interpreterError.Span, vmError.Span);
        Assert.Equal(interpreterError.SourceName, vmError.SourceName);
        Assert.Equal(interpreterError.SourceText, vmError.SourceText);
    }

    private static void AssertVmMatchesInterpreter(string source, VectorValue expected)
    {
        var syntax = Parse(source);
        var interpreterResult = new Interpreter().Execute(syntax, "scope.vec", source);
        var compilation = new BytecodeCompiler().Compile(syntax, "scope.vec", source);
        var vmResult = new VectorVirtualMachine().Execute(compilation.Program).Result;

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static BytecodeCompilationResult Compile(string source)
    {
        var syntax = Parse(source);
        return new BytecodeCompiler().Compile(syntax, "scope.vec", source);
    }

    private static CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }
}
