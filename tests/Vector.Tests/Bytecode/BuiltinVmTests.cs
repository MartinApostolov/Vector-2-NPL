using Vector.Core.Bytecode.Compiler;
using Vector.Core.Bytecode.Vm;
using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Vector.Core.Syntax;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Bytecode;

public sealed class BuiltinVmTests
{
    [Fact]
    public void VmCallsEveryGlobalBuiltinLikeInterpreter()
    {
        const string source = """
            [
                length([1, 2, 3]),
                length("A😀"),
                concat([1, 2], [3, 4]),
                text(42),
                number("12.5"),
                type([1]),
                range(1, 4)
            ];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(3),
                new NumberValue(2),
                new ListValue(new VectorValue[]
                {
                    new NumberValue(1),
                    new NumberValue(2),
                    new NumberValue(3),
                    new NumberValue(4)
                }),
                new TextValue("42"),
                new NumberValue(12.5),
                new TextValue("list"),
                new ListValue(new VectorValue[]
                {
                    new NumberValue(1),
                    new NumberValue(2),
                    new NumberValue(3)
                })
            }));
    }

    [Fact]
    public void PrintUsesVmHostAndRepresentativeBuiltinProgramMatchesInterpreter()
    {
        const string source = """
            print("VM");
            print(length([1, 2, 3]));
            print(type(42));
            range(1, 4);
            """;

        var interpreterOutput = new List<string>();
        var vmOutput = new List<string>();
        var syntax = Parse(source);

        var interpreterResult = new Interpreter(
            host: new VectorHost(interpreterOutput.Add))
            .Execute(syntax, "builtins-vm.vec", source);

        var compilation = new BytecodeCompiler().Compile(syntax, "builtins-vm.vec", source);
        var vmResult = new VectorVirtualMachine(
            host: new VectorHost(vmOutput.Add))
            .Execute(compilation.Program)
            .Result;

        var expectedResult = new ListValue(new VectorValue[]
        {
            new NumberValue(1),
            new NumberValue(2),
            new NumberValue(3)
        });

        Assert.Equal(expectedResult, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
        Assert.Equal(new[] { "VM", "3", "number" }, interpreterOutput.ToArray());
        Assert.Equal(interpreterOutput.ToArray(), vmOutput.ToArray());
    }

    [Fact]
    public void BuiltinCanBeStoredInVariableAndCalledLater()
    {
        AssertVmMatchesInterpreter(
            "let count = length; count([10, 20, 30, 40]);",
            new NumberValue(4));
    }

    [Fact]
    public void BuiltinFallbackWorksInsideBytecodeFunctionFrames()
    {
        AssertVmMatchesInterpreter(
            "function count(items) { return length(items); } count([1, 2, 3]);",
            new NumberValue(3));
    }

    [Fact]
    public void OrdinaryVariableLookupShadowsBuiltinFallback()
    {
        AssertVmMatchesInterpreter(
            "let range = 99; range;",
            new NumberValue(99));

        AssertEquivalentRuntimeFailure(
            "let print = 5; print();",
            DiagnosticCode.RuntimeTypeError);
    }

    [Fact]
    public void BuiltinFallbackReturnsAfterShadowingScopeEnds()
    {
        AssertVmMatchesInterpreter(
            "{ let type = 7; } type(42);",
            new TextValue("number"));
    }

    [Fact]
    public void WrongBuiltinArityIsRejectedBeforeArgumentsExecute()
    {
        const string source = "print(touched = 1, touched = 2);";
        var syntax = Parse(source);
        var interpreterEnvironment = EnvironmentWithTouched();
        var vmEnvironment = EnvironmentWithTouched();
        var interpreterOutput = new List<string>();
        var vmOutput = new List<string>();

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter(
                interpreterEnvironment,
                new VectorHost(interpreterOutput.Add))
            .Execute(syntax, "builtins-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "builtins-vm.vec", source);
        var vmError = Assert.Throws<RuntimeError>(() =>
            new VectorVirtualMachine(
                vmEnvironment,
                new VectorHost(vmOutput.Add))
            .Execute(compilation.Program));

        AssertEquivalentError(interpreterError, vmError, DiagnosticCode.ArgumentCountMismatch);
        Assert.Equal(new NumberValue(0), interpreterEnvironment.Get("touched", Span()));
        Assert.Equal(new NumberValue(0), vmEnvironment.Get("touched", Span()));
        Assert.Empty(interpreterOutput);
        Assert.Empty(vmOutput);
    }

    [Theory]
    [InlineData("length(12);")]
    [InlineData("number(\"not-a-number\");")]
    [InlineData("concat([1], 2);")]
    [InlineData("range(1.5, 4);")]
    public void BuiltinSemanticFailuresUseSameCallSiteDiagnosticAsInterpreter(string source)
    {
        AssertEquivalentRuntimeFailure(source, DiagnosticCode.RuntimeTypeError);
    }

    private static void AssertVmMatchesInterpreter(string source, VectorValue expected)
    {
        var syntax = Parse(source);
        var interpreterResult = new Interpreter().Execute(syntax, "builtins-vm.vec", source);
        var compilation = new BytecodeCompiler().Compile(syntax, "builtins-vm.vec", source);
        var vmResult = new VectorVirtualMachine().Execute(compilation.Program).Result;

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static void AssertEquivalentRuntimeFailure(string source, DiagnosticCode expectedCode)
    {
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter().Execute(syntax, "builtins-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "builtins-vm.vec", source);
        var vmError = Assert.Throws<RuntimeError>(() =>
            new VectorVirtualMachine().Execute(compilation.Program));

        AssertEquivalentError(interpreterError, vmError, expectedCode);
    }

    private static void AssertEquivalentError(
        RuntimeError interpreterError,
        RuntimeError vmError,
        DiagnosticCode expectedCode)
    {
        Assert.Equal(expectedCode, interpreterError.Code);
        Assert.Equal(interpreterError.Code, vmError.Code);
        Assert.Equal(interpreterError.Message, vmError.Message);
        Assert.Equal(interpreterError.Span, vmError.Span);
        Assert.Equal(interpreterError.SourceName, vmError.SourceName);
        Assert.Equal(interpreterError.SourceText, vmError.SourceText);
    }

    private static RuntimeEnvironment EnvironmentWithTouched()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("touched", new NumberValue(0), Span());
        return environment;
    }

    private static CompilationUnit Parse(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        Assert.Empty(parseResult.Diagnostics);
        return parseResult.Root;
    }

    private static SourceSpan Span() =>
        new(new SourcePosition(0, 1, 1), new SourcePosition(0, 1, 1));
}
