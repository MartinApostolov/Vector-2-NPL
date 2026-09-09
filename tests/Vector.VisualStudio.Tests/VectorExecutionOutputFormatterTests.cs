namespace Vector.VisualStudio.Tests;

using Vector.ExecutionProtocol;
using Vector.VisualStudio.Output;
using Xunit;

public sealed class VectorExecutionOutputFormatterTests
{
    [Fact]
    public void Format_IncludesSourceEngineOutputResultAndCompletion()
    {
        var request = new VectorExecutionRequest
        {
            Source = "print(\"hello\"); 42;",
            SourcePath = @"C:\workspace\sample.vec",
            Engine = VectorExecutionEngine.Vm,
        };
        var response = new VectorExecutionResponse
        {
            Success = true,
            Engine = VectorExecutionEngine.Vm,
            Operation = VectorExecutionOperation.Run,
            Output = ["hello"],
            Result = "42",
        };

        string formatted = VectorExecutionOutputFormatter.Format(request, response);

        Assert.Contains("Vector run (vm) — C:\\workspace\\sample.vec", formatted, StringComparison.Ordinal);
        Assert.Contains("hello", formatted, StringComparison.Ordinal);
        Assert.Contains("Result: 42", formatted, StringComparison.Ordinal);
        Assert.EndsWith("Vector operation completed successfully.", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesOneBasedDiagnosticLocations()
    {
        var request = new VectorExecutionRequest { Source = "let value = ;" };
        var response = new VectorExecutionResponse
        {
            Diagnostics =
            [
                new VectorProtocolDiagnostic(
                    "expected_expression",
                    "Error",
                    "Expected expression.",
                    "generated.vec",
                    new VectorProtocolRange(
                        new VectorProtocolPosition(12, 2, 4),
                        new VectorProtocolPosition(13, 2, 5))),
            ],
        };

        string formatted = VectorExecutionOutputFormatter.Format(request, response);

        Assert.Contains("generated.vec(3,5): Error expected_expression: Expected expression.", formatted, StringComparison.Ordinal);
        Assert.EndsWith("Vector operation failed.", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IncludesDisassemblyAndHostFailures()
    {
        var request = new VectorExecutionRequest
        {
            Source = "42;",
            Operation = VectorExecutionOperation.Disassemble,
            Engine = VectorExecutionEngine.Vm,
        };
        var response = new VectorExecutionResponse
        {
            Engine = VectorExecutionEngine.Vm,
            Operation = VectorExecutionOperation.Disassemble,
            Disassembly = "0000 Constant 42",
            HostFailure = new VectorHostFailure("host_crashed", "Process exited unexpectedly."),
        };

        string formatted = VectorExecutionOutputFormatter.Format(request, response);

        Assert.Contains("<unsaved Vector document>", formatted, StringComparison.Ordinal);
        Assert.Contains("0000 Constant 42", formatted, StringComparison.Ordinal);
        Assert.Contains("Execution host failure [host_crashed]: Process exited unexpectedly.", formatted, StringComparison.Ordinal);
    }
}
