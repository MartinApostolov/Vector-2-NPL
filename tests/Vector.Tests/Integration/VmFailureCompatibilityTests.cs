using Vector.Core.Diagnostics;
using Xunit;

namespace Vector.Tests.Integration;

public sealed class VmFailureCompatibilityTests
{
    public static IEnumerable<object[]> Failures =>
        new[]
        {
            Case("missing;", DiagnosticCode.UndefinedVariable),
            Case("let value = 1; let value = 2;", DiagnosticCode.VariableAlreadyDeclared),
            Case("1 + \"text\";", DiagnosticCode.RuntimeTypeError),
            Case("if 1 { 1; }", DiagnosticCode.RuntimeTypeError),
            Case("1 / 0;", DiagnosticCode.DivisionByZero),
            Case("1 % 0;", DiagnosticCode.DivisionByZero),
            Case("[1][1.5];", DiagnosticCode.InvalidListIndex),
            Case("[1][5];", DiagnosticCode.ListIndexOutOfRange),
            Case("let values = [1]; values[0] = values; values + [2];", DiagnosticCode.CyclicList),
            Case("[1, 2] + [3];", DiagnosticCode.VectorLengthMismatch),
            Case("function one(value) { return value; } one();", DiagnosticCode.ArgumentCountMismatch),
            Case("let value = 1; value();", DiagnosticCode.RuntimeTypeError)
        };

    [Theory]
    [MemberData(nameof(Failures))]
    public void RuntimeFailureDiagnosticsMatch(string source, DiagnosticCode expectedCode)
    {
        var interpreter = new Vector.Core.VectorEngine().Execute(source);
        var vm = new Vector.Core.VectorVmEngine().Execute(source);

        CompatibilityAssert.Equivalent(interpreter, vm);
        Assert.False(vm.Success);
        Assert.Equal(expectedCode, Assert.Single(vm.Diagnostics).Code);
    }

    private static object[] Case(string source, DiagnosticCode code) => new object[] { source, code };
}
