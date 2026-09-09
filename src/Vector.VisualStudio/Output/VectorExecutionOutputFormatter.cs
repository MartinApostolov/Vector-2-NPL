namespace Vector.VisualStudio.Output;

using System.Text;
using Vector.ExecutionProtocol;

public static class VectorExecutionOutputFormatter
{
    public static string Format(VectorExecutionRequest request, VectorExecutionResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        var output = new StringBuilder();
        string source = request.SourcePath ?? "<unsaved Vector document>";
        output.AppendLine($"Vector {response.Operation.ToString().ToLowerInvariant()} "
            + $"({response.Engine.ToString().ToLowerInvariant()}) — {source}");

        foreach (string line in response.Output)
        {
            output.AppendLine(line);
        }

        if (response.Result is not null)
        {
            output.AppendLine($"Result: {response.Result}");
        }

        foreach (VectorProtocolDiagnostic diagnostic in response.Diagnostics)
        {
            string diagnosticSource = diagnostic.SourceName ?? source;
            output.AppendLine(
                $"{diagnosticSource}({diagnostic.Range.Start.Line + 1},{diagnostic.Range.Start.Character + 1}): "
                + $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
        }

        if (response.Disassembly is not null)
        {
            output.Append(response.Disassembly);
            if (!response.Disassembly.EndsWith('\n'))
            {
                output.AppendLine();
            }
        }

        if (response.HostFailure is not null)
        {
            output.AppendLine($"Execution host failure [{response.HostFailure.Code}]: {response.HostFailure.Message}");
        }

        output.AppendLine(response.Success ? "Vector operation completed successfully." : "Vector operation failed.");
        return output.ToString().TrimEnd();
    }
}
