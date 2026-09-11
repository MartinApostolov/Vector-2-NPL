namespace Vector.Npl.Semantics.Tests;

using Vector.Npl.Semantics.Tests.Benchmarking;
using Xunit;

public sealed class ComparisonBenchmarkLoaderTests
{
    public static TheoryData<string, string> InvalidCases => new()
    {
        { "not json", "BENCHMARK_JSON_MALFORMED" },
        { "[]", "BENCHMARK_CASE_OBJECT_REQUIRED" },
        { RejectCase().Replace("\"id\":\"case-1\"", "\"id\":\"case-1\",\"id\":\"case-2\""), "BENCHMARK_MEMBER_DUPLICATE" },
        { RejectCase().Replace("\"tags\":", "\"extra\":true,\"tags\":"), "BENCHMARK_MEMBER_UNKNOWN" },
        { RejectCase().Replace("\"case-1\"", "\"   \""), "BENCHMARK_STRING_BLANK" },
        { RejectCase().Replace("\"en\"", "\"bg\""), "BENCHMARK_LANGUAGE_UNSUPPORTED" },
        { RejectCase().Replace("\"ambiguous request\"", "\"  \""), "BENCHMARK_STRING_BLANK" },
        { RejectCase().Replace("\"reject\"", "\"maybe\""), "BENCHMARK_EXPECTATION_UNSUPPORTED" },
        { RejectCase().Replace(",\"tags\":[\"reject\"]", string.Empty), "BENCHMARK_TAGS_REQUIRED" },
        { RejectCase().Replace("[\"reject\"]", "[]"), "BENCHMARK_TAGS_EMPTY" },
        { RejectCase().Replace("[\"reject\"]", "[\" \"]"), "BENCHMARK_TAG_INVALID" },
        { SemanticCase().Replace(",\"expected\":" + ValidExpected(), string.Empty), "BENCHMARK_SEMANTIC_EXPECTED_REQUIRED" },
        { SemanticCase().Replace(",\"tags\":", ",\"rejectReason\":\"not-allowed\",\"tags\":"), "BENCHMARK_SEMANTIC_REJECT_REASON_FORBIDDEN" },
        { RejectCase().Replace(",\"tags\":", ",\"expected\":" + ValidExpected() + ",\"tags\":"), "BENCHMARK_REJECT_EXPECTED_FORBIDDEN" },
        { RejectCase().Replace(",\"rejectReason\":\"ambiguous\"", string.Empty), "BENCHMARK_MEMBER_REQUIRED" },
        { RejectCase().Replace("\"ambiguous\"", "\"  \""), "BENCHMARK_STRING_BLANK" },
        { RejectCase().Replace(",\"tags\":", ",\"pair\":1,\"tags\":"), "BENCHMARK_OPTIONAL_STRING_INVALID" },
        { RejectCase().Replace(",\"tags\":", ",\"notes\":false,\"tags\":"), "BENCHMARK_OPTIONAL_STRING_INVALID" },
        { SemanticCase().Replace(ValidExpected(), "{\"type\":\"futureNode\"}"), "BENCHMARK_EXPECTED_IR_INVALID" },
        { SemanticCase().Replace(ValidExpected(), "{\"type\":\"currentItem\"}"), "BENCHMARK_EXPECTED_COMPARISON_REQUIRED" },
    };

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void StrictSchema_RejectsInvalidInMemoryCase(string jsonl, string expectedCode)
    {
        ComparisonBenchmarkException exception = Assert.Throws<ComparisonBenchmarkException>(
            () => ComparisonBenchmarkLoader.LoadText(jsonl));

        Assert.Contains(exception.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void DuplicateCaseIdWithinSplit_IsRejected()
    {
        string jsonl = RejectCase() + "\n" + RejectCase().Replace("ambiguous request", "different request");

        ComparisonBenchmarkException exception = Assert.Throws<ComparisonBenchmarkException>(
            () => ComparisonBenchmarkLoader.LoadText(jsonl));

        Assert.Contains(exception.Issues, issue => issue.Code == "BENCHMARK_ID_DUPLICATE");
    }

    [Fact]
    public void DuplicateNormalizedInputWithinSplit_IsRejected()
    {
        string second = RejectCase()
            .Replace("case-1", "case-2")
            .Replace("ambiguous request", "  AMBIGUOUS\\trequest  ");
        string jsonl = RejectCase() + "\n" + second;

        ComparisonBenchmarkException exception = Assert.Throws<ComparisonBenchmarkException>(
            () => ComparisonBenchmarkLoader.LoadText(jsonl));

        Assert.Contains(exception.Issues, issue => issue.Code == "BENCHMARK_INPUT_DUPLICATE");
    }

    [Fact]
    public void ExpectedSemanticIrWithUnknownMember_IsRejectedByP01Contract()
    {
        string invalidExpected = ValidExpected().Replace(
            "\"kind\":\"GT\"",
            "\"kind\":\"GT\",\"operator\":\">\"");
        string jsonl = SemanticCase().Replace(ValidExpected(), invalidExpected);

        ComparisonBenchmarkException exception = Assert.Throws<ComparisonBenchmarkException>(
            () => ComparisonBenchmarkLoader.LoadText(jsonl));

        BenchmarkValidationIssue issue = Assert.Single(
            exception.Issues,
            issue => issue.Code == "BENCHMARK_EXPECTED_IR_INVALID");
        Assert.Contains("JSON_MEMBER_UNKNOWN", issue.Message, StringComparison.Ordinal);
        Assert.Equal("$[1].expected.operator", issue.Path);
    }

    private static string RejectCase() =>
        "{\"id\":\"case-1\",\"language\":\"en\",\"input\":\"ambiguous request\",\"expectation\":\"reject\",\"rejectReason\":\"ambiguous\",\"tags\":[\"reject\"]}";

    private static string SemanticCase() =>
        "{\"id\":\"case-1\",\"language\":\"en\",\"input\":\"score is greater than 10\",\"expectation\":\"semantic\",\"expected\":" +
        ValidExpected() +
        ",\"tags\":[\"GT\"]}";

    private static string ValidExpected() =>
        "{\"type\":\"comparison\",\"kind\":\"GT\",\"left\":{\"type\":\"identifier\",\"name\":\"score\"},\"right\":{\"type\":\"numberLiteral\",\"value\":10}}";
}
