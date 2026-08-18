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

public sealed class VectorOperatorBytecodeTests
{
    [Fact]
    public void NumericListAdditionMatchesInterpreter()
    {
        AssertVmMatchesInterpreter(
            "[1, 2, 3] + [4, 5, 6];",
            Numbers(5, 7, 9));
    }

    [Fact]
    public void NumericListSubtractionMatchesInterpreter()
    {
        AssertVmMatchesInterpreter(
            "[5, 7, 9] - [1, 2, 3];",
            Numbers(4, 5, 6));
    }

    [Fact]
    public void ScalarMultiplicationWorksInBothDirections()
    {
        AssertVmMatchesInterpreter(
            "[[1, 2, 3] * 2, 3 * [4, 5]];",
            new ListValue(new VectorValue[]
            {
                Numbers(2, 4, 6),
                Numbers(12, 15)
            }));
    }

    [Fact]
    public void IndexedMutationImmediatelyChangesVectorEligibility()
    {
        AssertEquivalentRuntimeFailure(
            "let values = [1, 2, 3]; values[1] = \"two\"; values * 2;",
            DiagnosticCode.RuntimeTypeError);
    }

    [Fact]
    public void RestoringNumericElementRestoresVectorEligibility()
    {
        const string source = "let values = [1, 2, 3]; values[1] = \"two\"; values[1] = 10; values * 2;";

        AssertVmMatchesInterpreter(source, Numbers(2, 20, 6));
    }

    [Fact]
    public void VectorLengthMismatchMatchesInterpreterDiagnostic()
    {
        AssertEquivalentRuntimeFailure(
            "[1, 2] + [3];",
            DiagnosticCode.VectorLengthMismatch);
    }

    [Theory]
    [InlineData("[1, \"two\"] + [3, 4];")]
    [InlineData("[1, nothing] - [3, 4];")]
    [InlineData("[1, true] * 2;")]
    public void NonNumericVectorOperationsMatchInterpreterDiagnostic(string source)
    {
        AssertEquivalentRuntimeFailure(source, DiagnosticCode.RuntimeTypeError);
    }

    private static ListValue Numbers(params double[] values) =>
        new(values.Select(value => (VectorValue)new NumberValue(value)));

    private static void AssertVmMatchesInterpreter(string source, VectorValue expected)
    {
        var syntax = Parse(source);
        var interpreterResult = new Interpreter().Execute(syntax, "vectors-vm.vec", source);
        var compilation = new BytecodeCompiler().Compile(syntax, "vectors-vm.vec", source);
        var vmResult = new VectorVirtualMachine().Execute(compilation.Program).Result;

        Assert.Equal(expected, interpreterResult);
        Assert.Equal(interpreterResult, vmResult);
    }

    private static void AssertEquivalentRuntimeFailure(string source, DiagnosticCode expectedCode)
    {
        var syntax = Parse(source);

        var interpreterError = Assert.Throws<RuntimeError>(() =>
            new Interpreter().Execute(syntax, "vectors-vm.vec", source));

        var compilation = new BytecodeCompiler().Compile(syntax, "vectors-vm.vec", source);
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
