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

public sealed class ClosureBytecodeTests
{
    [Fact]
    public void FunctionReadsAndMutatesCapturedOuterBinding()
    {
        const string source = """
            let value = 10;
            function update(amount) {
                value = value + amount;
                return value;
            }
            [update(5), update(2), value];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(15),
                new NumberValue(17),
                new NumberValue(17)
            }));
    }

    [Fact]
    public void FunctionLocalShadowingLeavesCapturedOuterBindingUnchanged()
    {
        const string source = """
            let value = 10;
            function change() {
                let value = 20;
                value = 30;
                return value;
            }
            [change(), value];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(30),
                new NumberValue(10)
            }));
    }

    [Fact]
    public void ClosureKeepsEscapedBlockEnvironmentAlive()
    {
        const string source = """
            let saved = nothing;
            {
                let captured = 12;
                function addCaptured(value) { return captured + value; }
                saved = addCaptured;
            }
            saved(5);
            """;

        AssertVmMatchesInterpreter(source, new NumberValue(17));
    }

    [Fact]
    public void SeparateOuterCallsCreateSeparateCapturedEnvironments()
    {
        const string source = """
            function makeAdder(base) {
                function add(value) { return base + value; }
                return add;
            }
            let add2 = makeAdder(2);
            let add10 = makeAdder(10);
            [add2(5), add10(5)];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(7),
                new NumberValue(15)
            }));
    }

    [Fact]
    public void CounterClosureRetainsMutableStateAcrossCalls()
    {
        const string source = """
            function makeCounter() {
                let value = 0;
                function next() {
                    value = value + 1;
                    return value;
                }
                return next;
            }

            let counter = makeCounter();
            [counter(), counter(), counter()];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(1),
                new NumberValue(2),
                new NumberValue(3)
            }));
    }

    [Fact]
    public void MultipleClosuresFromSameInvocationShareCapturedState()
    {
        const string source = """
            function makePair() {
                let value = 0;
                function increase() {
                    value = value + 1;
                    return value;
                }
                function read() { return value; }
                return [increase, read];
            }

            let pair = makePair();
            [pair[0](), pair[1](), pair[0](), pair[1]()];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(1),
                new NumberValue(1),
                new NumberValue(2),
                new NumberValue(2)
            }));
    }

    [Fact]
    public void NestedFunctionDoesNotLeakUnlessEscaped()
    {
        const string source = "function outer() { function inner() { return 1; } return inner(); } outer(); inner();";

        AssertEquivalentRuntimeFailure(source, DiagnosticCode.UndefinedVariable);
    }

    [Fact]
    public void ReturnedClosureCanCaptureParameterAndFunctionLocalTogether()
    {
        const string source = """
            function make(base) {
                let offset = 1;
                function next(value) {
                    offset = offset + 1;
                    return base + offset + value;
                }
                return next;
            }
            let add = make(10);
            [add(5), add(5)];
            """;

        AssertVmMatchesInterpreter(
            source,
            new ListValue(new VectorValue[]
            {
                new NumberValue(17),
                new NumberValue(18)
            }));
    }

    private static void AssertVmMatchesInterpreter(string source, VectorValue expected)
    {
        var syntax = Parse(source);
        var interpreterResult = new Interpreter().Execute(syntax, "closures-vm.vec", source);
        var compilation = new BytecodeCompiler().Compile(syntax, "closures-vm.vec", source);
        var vmResult = new VectorVirtualMachine().Execute(compilation.Program).Result;

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static void AssertEquivalentRuntimeFailure(string source, DiagnosticCode expectedCode)
    {
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter().Execute(syntax, "closures-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "closures-vm.vec", source);
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
