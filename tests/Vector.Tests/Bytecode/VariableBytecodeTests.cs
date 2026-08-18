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

public sealed class VariableBytecodeTests
{
    [Fact]
    public void CompilerEmitsVariableDeclarationLookupAndAssignmentInstructions()
    {
        const string source = "let value = 10; value = value + 5; value;";
        var compilation = Compile(source);
        var chunk = compilation.Program.EntryPoint;

        Assert.Equal(new[] { "value" }, chunk.Names);
        Assert.Equal(
            new[]
            {
                OpCode.Constant,
                OpCode.DeclareVariable,
                OpCode.Pop,
                OpCode.GetVariable,
                OpCode.Constant,
                OpCode.Add,
                OpCode.AssignVariable,
                OpCode.GetVariable,
                OpCode.Pop,
                OpCode.GetVariable,
                OpCode.Halt
            },
            chunk.Instructions.Select(instruction => instruction.OpCode));
    }

    [Fact]
    public void VmExecutesDeclarationReadAndAssignmentLikeInterpreter()
    {
        const string source = "let value = 10; value = value + 5; value;";

        AssertVmMatchesInterpreter(source, new NumberValue(15));
    }

    [Fact]
    public void AssignmentExpressionReturnsAssignedValue()
    {
        const string source = "let value = 1; (value = 7) + value;";

        AssertVmMatchesInterpreter(source, new NumberValue(14));
    }

    [Fact]
    public void DeclarationInitializerIsEvaluatedBeforeNewBindingIsDeclared()
    {
        const string source = """
            let result = 0;
            let value = 10;
            {
                let value = value + 5;
                result = value;
            }
            result;
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(15));
    }

    [Fact]
    public void UndefinedVariableReadMatchesInterpreterDiagnostic()
    {
        AssertEquivalentRuntimeFailure("missing;", DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void UndefinedVariableAssignmentMatchesInterpreterDiagnostic()
    {
        AssertEquivalentRuntimeFailure("missing = 1;", DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void DuplicateDeclarationMatchesInterpreterDiagnostic()
    {
        AssertEquivalentRuntimeFailure(
            "let value = 1; let value = 2;",
            DiagnosticCode.VariableAlreadyDeclared);
    }

    private static void AssertVmMatchesInterpreter(string source, VectorValue expected)
    {
        var syntax = Parse(source);
        var interpreterResult = new Interpreter().Execute(syntax, "variables.vec", source);
        var vmResult = ExecuteVm(syntax, source);

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static void AssertEquivalentRuntimeFailure(string source, DiagnosticCode expectedCode)
    {
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter().Execute(syntax, "variables.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "variables.vec", source);
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
        var compilation = new BytecodeCompiler().Compile(syntax, "variables.vec", source);
        return new VectorVirtualMachine().Execute(compilation.Program).Result;
    }

    private static BytecodeCompilationResult Compile(string source)
    {
        var syntax = Parse(source);
        return new BytecodeCompiler().Compile(syntax, "variables.vec", source);
    }

    private static CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }
}
