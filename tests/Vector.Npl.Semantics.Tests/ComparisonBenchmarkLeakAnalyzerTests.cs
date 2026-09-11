namespace Vector.Npl.Semantics.Tests;

using Vector.Npl.Semantics;
using Vector.Npl.Semantics.Tests.Benchmarking;
using Xunit;

public sealed class ComparisonBenchmarkLeakAnalyzerTests
{
    [Fact]
    public void Analyze_DetectsCrossSplitDuplicateIdInputAndDisposition()
    {
        ComparisonBenchmarkCase developmentCase =
            SemanticCase("same-id", "Score  is greater than 10.", "score", 10);
        ComparisonBenchmarkCase gateCase =
            SemanticCase("same-id", "  score IS GREATER THAN 10.  ", "score", 10);

        IReadOnlyList<BenchmarkLeakIssue> issues = ComparisonBenchmarkLeakAnalyzer.Analyze(
            Split("development", developmentCase),
            Split("gate", gateCase));

        Assert.Contains(issues, issue => issue.Code == "CROSS_SPLIT_ID_DUPLICATE");
        Assert.Contains(issues, issue => issue.Code == "CROSS_SPLIT_INPUT_DUPLICATE");
        Assert.Contains(issues, issue => issue.Code == "CROSS_SPLIT_INPUT_DISPOSITION_DUPLICATE");
    }

    [Fact]
    public void Analyze_DetectsTrivialSurfaceTemplateReuseWithDifferentValues()
    {
        ComparisonBenchmarkCase developmentCase =
            SemanticCase("dev-1", "score is greater than 10.", "score", 10);
        ComparisonBenchmarkCase gateCase =
            SemanticCase("gate-1", "temperature is greater than 25.", "temperature", 25);

        BenchmarkLeakIssue issue = Assert.Single(
            ComparisonBenchmarkLeakAnalyzer.Analyze(
                Split("development", developmentCase),
                Split("gate", gateCase)),
            issue => issue.Code == "CROSS_SPLIT_TEMPLATE_OVERLAP");

        Assert.Equal("<ID> IS GREATER THAN <NUMBER>.", issue.Value);
        Assert.Equal("dev-1", issue.DevelopmentCaseId);
        Assert.Equal("gate-1", issue.GateCaseId);
    }

    [Fact]
    public void SurfaceTemplate_ReplacesQuotedTextAndBooleanLiterals()
    {
        var textCase = new ComparisonBenchmarkCase(
            "text",
            "en",
            "status differs from \"ready\".",
            ComparisonBenchmarkExpectation.Semantic,
            new Comparison(ComparisonKind.NEQ, new Identifier("status"), new TextLiteral("ready")),
            null,
            ["NEQ"],
            null,
            null);
        var booleanCase = new ComparisonBenchmarkCase(
            "boolean",
            "en",
            "enabled is true.",
            ComparisonBenchmarkExpectation.Semantic,
            new Comparison(ComparisonKind.EQ, new Identifier("enabled"), new BooleanLiteral(true)),
            null,
            ["EQ"],
            null,
            null);

        Assert.Equal(
            "<ID> DIFFERS FROM <TEXT>.",
            ComparisonBenchmarkLeakAnalyzer.CreateSurfaceTemplate(textCase));
        Assert.Equal(
            "<ID> IS <BOOLEAN>.",
            ComparisonBenchmarkLeakAnalyzer.CreateSurfaceTemplate(booleanCase));
    }

    private static ComparisonBenchmarkCase SemanticCase(
        string id,
        string input,
        string identifier,
        double number) => new(
            id,
            "en",
            input,
            ComparisonBenchmarkExpectation.Semantic,
            new Comparison(ComparisonKind.GT, new Identifier(identifier), new NumberLiteral(number)),
            null,
            ["GT"],
            null,
            null);

    private static ComparisonBenchmarkSplit Split(string name, params ComparisonBenchmarkCase[] cases) =>
        new(name, Array.AsReadOnly(cases));
}
