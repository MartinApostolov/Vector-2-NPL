using Vector.Core.Bytecode;
using Vector.Core.Bytecode.Compiler;
using Vector.Core.Bytecode.Vm;
using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Bytecode;

public sealed class VirtualMachineExpressionTests
{
    public static TheoryData<string, VectorValue> SuccessfulExpressions => new()
    {
        { "12.5;", new NumberValue(12.5) },
        { "\"hello\";", new TextValue("hello") },
        { "true;", new BooleanValue(true) },
        { "false;", new BooleanValue(false) },
        { "nothing;", NothingValue.Instance },
        { "((7));", new NumberValue(7) },
        { "--5;", new NumberValue(5) },
        { "not not true;", new BooleanValue(true) },
        { "2 + 3 * 4;", new NumberValue(14) },
        { "(2 + 3) * 4;", new NumberValue(20) },
        { "5 / 2;", new NumberValue(2.5) },
        { "10 % 3;", new NumberValue(1) },
        { "\"Hello \" + \"Vector\";", new TextValue("Hello Vector") },
        { "1 < 2;", new BooleanValue(true) },
        { "2 <= 2;", new BooleanValue(true) },
        { "3 > 2;", new BooleanValue(true) },
        { "3 >= 3;", new BooleanValue(true) },
        { "5 == 5;", new BooleanValue(true) },
        { "5 != 6;", new BooleanValue(true) },
        { "\"5\" == 5;", new BooleanValue(false) },
        { "nothing == nothing;", new BooleanValue(true) }
    };

    [Theory]
    [MemberData(nameof(SuccessfulExpressions))]
    public void VmMatchesInterpreterForCoreExpressions(string source, VectorValue expected)
    {
        var syntax = Parse(source);

        var interpreterResult = new Interpreter().Execute(syntax, "expression.vec", source);
        var compilation = new BytecodeCompiler().Compile(syntax, "expression.vec", source);
        var vmResult = new VectorVirtualMachine().Execute(compilation.Program).Result;

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    [Fact]
    public void VmReturnsFinalExpressionWithoutLeakingEarlierStatementValues()
    {
        const string source = "1; 2 + 3; 10 - 3;";

        Assert.Equal(new NumberValue(7), ExecuteVm(source));
    }

    [Fact]
    public void VmReturnsNothingForEmptyProgram()
    {
        Assert.Equal(NothingValue.Instance, ExecuteVm(string.Empty));
    }

    [Theory]
    [InlineData("-\"5\";")]
    [InlineData("not 1;")]
    [InlineData("5 + \"2\";")]
    [InlineData("\"8\" / 2;")]
    [InlineData("\"a\" < \"b\";")]
    public void VmMatchesInterpreterRuntimeTypeFailures(string source)
    {
        AssertEquivalentRuntimeFailure(source, DiagnosticCode.RuntimeTypeError);
    }

    [Theory]
    [InlineData("1 / 0;")]
    [InlineData("1 / -0;")]
    [InlineData("1 % 0;")]
    public void VmMatchesInterpreterZeroDivisorFailures(string source)
    {
        AssertEquivalentRuntimeFailure(source, DiagnosticCode.DivisionByZero);
    }

    [Fact]
    public void VmAttachesCompiledSourceInformationToRuntimeErrors()
    {
        const string source = "1 / 0;";
        var syntax = Parse(source);
        var compilation = new BytecodeCompiler().Compile(syntax, "sample.vec", source);

        var error = Assert.Throws<RuntimeError>(() =>
            new VectorVirtualMachine().Execute(compilation.Program));

        Assert.Equal("sample.vec", error.SourceName);
        Assert.Equal(source, error.SourceText);
        Assert.Equal(4, error.Span.Start.Offset);
        Assert.Equal(5, error.Span.End.Offset);
    }

    [Fact]
    public void VmRejectsMalformedBytecodeStackUnderflow()
    {
        var builder = new BytecodeBuilder();
        builder.Emit(OpCode.Add, Span(0, 1));
        builder.Emit(OpCode.Halt, Span(1, 1));

        var error = Assert.Throws<InvalidOperationException>(() =>
            new VectorVirtualMachine().Execute(new BytecodeProgram(builder.Build())));

        Assert.Contains("stack underflow", error.Message.ToLowerInvariant());
    }

    private static void AssertEquivalentRuntimeFailure(string source, DiagnosticCode expectedCode)
    {
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter().Execute(syntax, "failure.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "failure.vec", source);
        var vmError = Assert.Throws<RuntimeError>(() =>
            new VectorVirtualMachine().Execute(compilation.Program));

        Assert.Equal(expectedCode, interpreterError.Code);
        Assert.Equal(interpreterError.Code, vmError.Code);
        Assert.Equal(interpreterError.Message, vmError.Message);
        Assert.Equal(interpreterError.Span, vmError.Span);
        Assert.Equal(interpreterError.SourceName, vmError.SourceName);
        Assert.Equal(interpreterError.SourceText, vmError.SourceText);
    }

    private static VectorValue ExecuteVm(string source)
    {
        var syntax = Parse(source);
        var compilation = new BytecodeCompiler().Compile(syntax, "vm-test.vec", source);
        return new VectorVirtualMachine().Execute(compilation.Program).Result;
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
