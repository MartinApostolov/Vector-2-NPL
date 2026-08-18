using Vector.Core.Bytecode.Compiler;
using Vector.Core.Bytecode.Vm;
using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Vector.Core.Syntax;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Bytecode;

public sealed class NativeCallableVmTests
{
    [Fact]
    public void NativeFunctionExecutesThroughVmCallableBridge()
    {
        const string source = "square(5);";
        var function = new NativeFunction(
            "square",
            1,
            (_, arguments) =>
            {
                var value = NativeValueConverter.ToNumber(arguments[0], "value");
                return NativeValueConverter.FromNumber(value * value);
            });

        AssertVmMatchesInterpreterWithNative(source, function, new NumberValue(25));
    }

    [Fact]
    public void WrongNativeArityIsRejectedBeforeArgumentsExecute()
    {
        const string source = "identity(touched = 1, touched = 2);";
        var syntax = Parse(source);
        var interpreterEnvironment = EnvironmentWithNative(
            new NativeFunction("identity", 1, (_, arguments) => arguments[0]));
        var vmEnvironment = EnvironmentWithNative(
            new NativeFunction("identity", 1, (_, arguments) => arguments[0]));
        interpreterEnvironment.Declare("touched", new NumberValue(0), Span());
        vmEnvironment.Declare("touched", new NumberValue(0), Span());

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter(interpreterEnvironment)
                .Execute(syntax, "native-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "native-vm.vec", source);
        var vmError = Assert.Throws<RuntimeError>(() =>
            new VectorVirtualMachine(vmEnvironment)
                .Execute(compilation.Program));

        AssertEquivalentError(interpreterError, vmError, DiagnosticCode.ArgumentCountMismatch);
        Assert.Equal(new NumberValue(0), interpreterEnvironment.Get("touched", Span()));
        Assert.Equal(new NumberValue(0), vmEnvironment.Get("touched", Span()));
    }

    [Fact]
    public void DeliberateNativeFailureUsesSameCallSiteDiagnosticAsInterpreter()
    {
        const string source = "nativeFail(1);";
        Func<NativeFunction> factory = () => new NativeFunction(
            "nativeFail",
            1,
            (_, _) => throw new NativeRuntimeException(
                DiagnosticCode.RuntimeTypeError,
                "Deliberate native failure."));

        AssertEquivalentNativeFailure(source, factory, DiagnosticCode.RuntimeTypeError);
    }

    [Fact]
    public void UnexpectedNativeExceptionBecomesSafeRuntimeFailureOnVm()
    {
        const string source = "explode();";
        Func<NativeFunction> factory = () => new NativeFunction(
            "explode",
            0,
            (_, _) => throw new InvalidOperationException("host implementation secret"));

        var (interpreterError, vmError) = ExecuteNativeFailure(source, factory);

        AssertEquivalentError(interpreterError, vmError, DiagnosticCode.NativeRuntimeFailure);
        Assert.Equal("Native function 'explode' failed.", vmError.Message);
        Assert.DoesNotContain("secret", vmError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", vmError.Message);
    }

    [Fact]
    public void NativeCallableReceivesInputCapableVmHostContext()
    {
        const string source = "readNative();";
        var interpreterInput = new Queue<string?>(new string?[] { "Ada" });
        var vmInput = new Queue<string?>(new string?[] { "Ada" });
        var interpreterHost = new VectorInputHost(null, () => interpreterInput.Dequeue());
        var vmHost = new VectorInputHost(null, () => vmInput.Dequeue());

        var interpreterFunction = CreateReadNative();
        var vmFunction = CreateReadNative();
        var interpreterEnvironment = EnvironmentWithNative(interpreterFunction);
        var vmEnvironment = EnvironmentWithNative(vmFunction);
        var syntax = Parse(source);

        var interpreterResult = new Interpreter(interpreterEnvironment, interpreterHost)
            .Execute(syntax, "native-vm.vec", source);

        var compilation = new BytecodeCompiler().Compile(syntax, "native-vm.vec", source);
        var vmResult = new VectorVirtualMachine(vmEnvironment, vmHost)
            .Execute(compilation.Program)
            .Result;

        Assert.Equal(new TextValue("Ada"), interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
        Assert.Empty(interpreterInput);
        Assert.Empty(vmInput);
    }

    [Fact]
    public void NativeCallableSharesCurrentVmEnvironment()
    {
        const string source = "function check() { let value = 21; return readCurrent(); } check();";
        Func<NativeFunction> factory = () => new NativeFunction(
            "readCurrent",
            0,
            (interpreter, _) => interpreter.CurrentEnvironment.Get("value", Span()));

        var interpreterEnvironment = EnvironmentWithNative(factory());
        var vmEnvironment = EnvironmentWithNative(factory());
        var syntax = Parse(source);

        var interpreterResult = new Interpreter(interpreterEnvironment)
            .Execute(syntax, "native-vm.vec", source);

        var compilation = new BytecodeCompiler().Compile(syntax, "native-vm.vec", source);
        var vmResult = new VectorVirtualMachine(vmEnvironment)
            .Execute(compilation.Program)
            .Result;

        Assert.Equal(new NumberValue(21), interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    [Fact]
    public void NativeCallableCanMutateCurrentVmEnvironment()
    {
        const string source = "function change() { let value = 4; bump(); return value; } change();";
        Func<NativeFunction> factory = () => new NativeFunction(
            "bump",
            0,
            (interpreter, _) =>
            {
                var current = interpreter.CurrentEnvironment.Get("value", Span());
                var number = NativeValueConverter.ToNumber(current, "value");
                interpreter.CurrentEnvironment.Assign(
                    "value",
                    NativeValueConverter.FromNumber(number + 1),
                    Span());
                return NothingValue.Instance;
            });

        var interpreterEnvironment = EnvironmentWithNative(factory());
        var vmEnvironment = EnvironmentWithNative(factory());
        var syntax = Parse(source);

        var interpreterResult = new Interpreter(interpreterEnvironment)
            .Execute(syntax, "native-vm.vec", source);

        var compilation = new BytecodeCompiler().Compile(syntax, "native-vm.vec", source);
        var vmResult = new VectorVirtualMachine(vmEnvironment)
            .Execute(compilation.Program)
            .Result;

        Assert.Equal(new NumberValue(5), interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    [Fact]
    public void NativeArgumentTypeFailureMatchesInterpreterAtCallSite()
    {
        const string source = "double(\"5\");";
        Func<NativeFunction> factory = () => new NativeFunction(
            "double",
            1,
            (_, arguments) => NativeValueConverter.FromNumber(
                NativeValueConverter.ToNumber(arguments[0], "value") * 2));

        AssertEquivalentNativeFailure(source, factory, DiagnosticCode.RuntimeTypeError);
    }

    private static NativeFunction CreateReadNative() =>
        new(
            "readNative",
            0,
            (interpreter, _) =>
            {
                if (interpreter.Host is not IVectorInputHost inputHost)
                {
                    throw new NativeRuntimeException(
                        DiagnosticCode.NativeRuntimeFailure,
                        "Input-capable host is required.");
                }

                return NativeValueConverter.FromNullableText(inputHost.ReadLine());
            });

    private static void AssertVmMatchesInterpreterWithNative(
        string source,
        NativeFunction function,
        VectorValue expected)
    {
        var syntax = Parse(source);
        var interpreterEnvironment = EnvironmentWithNative(function);
        var vmEnvironment = EnvironmentWithNative(function);

        var interpreterResult = new Interpreter(interpreterEnvironment)
            .Execute(syntax, "native-vm.vec", source);
        var compilation = new BytecodeCompiler().Compile(syntax, "native-vm.vec", source);
        var vmResult = new VectorVirtualMachine(vmEnvironment)
            .Execute(compilation.Program)
            .Result;

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static void AssertEquivalentNativeFailure(
        string source,
        Func<NativeFunction> factory,
        DiagnosticCode expectedCode)
    {
        var (interpreterError, vmError) = ExecuteNativeFailure(source, factory);
        AssertEquivalentError(interpreterError, vmError, expectedCode);
    }

    private static (RuntimeError InterpreterError, RuntimeError VmError) ExecuteNativeFailure(
        string source,
        Func<NativeFunction> factory)
    {
        var syntax = Parse(source);
        var interpreterEnvironment = EnvironmentWithNative(factory());
        var vmEnvironment = EnvironmentWithNative(factory());

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter(interpreterEnvironment)
                .Execute(syntax, "native-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "native-vm.vec", source);
        var vmError = Assert.Throws<RuntimeError>(() =>
            new VectorVirtualMachine(vmEnvironment)
                .Execute(compilation.Program));

        return (interpreterError, vmError);
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

    private static RuntimeEnvironment EnvironmentWithNative(NativeFunction function)
    {
        var environment = new RuntimeEnvironment();
        environment.Declare(function.Name, function, Span());
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
