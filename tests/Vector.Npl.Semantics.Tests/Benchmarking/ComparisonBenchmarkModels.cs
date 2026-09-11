namespace Vector.Npl.Semantics.Tests.Benchmarking;

using Vector.Npl.Semantics;

internal enum ComparisonBenchmarkExpectation
{
    Semantic,
    Reject,
}

internal sealed record ComparisonBenchmarkCase(
    string Id,
    string Language,
    string Input,
    ComparisonBenchmarkExpectation Expectation,
    SemanticExpression? Expected,
    string? RejectReason,
    IReadOnlyList<string> Tags,
    string? Pair,
    string? Notes);

internal sealed record ComparisonBenchmarkSplit(
    string SourceName,
    IReadOnlyList<ComparisonBenchmarkCase> Cases);

internal sealed record BenchmarkValidationIssue(string Code, string Path, string Message);

internal sealed class ComparisonBenchmarkException : FormatException
{
    public ComparisonBenchmarkException(IReadOnlyList<BenchmarkValidationIssue> issues)
        : base("The comparison benchmark is invalid.")
    {
        Issues = issues;
    }

    public IReadOnlyList<BenchmarkValidationIssue> Issues { get; }
}

internal sealed record BenchmarkLeakIssue(
    string Code,
    string DevelopmentCaseId,
    string GateCaseId,
    string Value);
