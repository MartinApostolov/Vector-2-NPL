namespace Vector.Npl.Semantics.Tests;

using Vector.Npl.Semantics;
using Vector.Npl.Semantics.Tests.Benchmarking;
using Xunit;

public sealed class ComparisonBenchmarkCorpusTests
{
    private const string SealedGateHash = "26cb935530866054fdbbaf026d96cd7c79dc83fe70e514669247ca30bdb077c9";

    [Fact]
    public void SuppliedBenchmarkFiles_LoadWithFrozenCaseCounts()
    {
        ComparisonBenchmarkSplit development = LoadDevelopment();
        ComparisonBenchmarkSplit gate = LoadGate();

        Assert.Equal(60, development.Cases.Count);
        Assert.Equal(72, gate.Cases.Count);
    }

    [Fact]
    public void SealedGate_HasRequiredClassAndRejectionCounts()
    {
        IReadOnlyDictionary<string, int> counts =
            ComparisonBenchmarkManifestVerifier.CountDispositions(LoadGate());

        Assert.Equal(10, counts["GT"]);
        Assert.Equal(10, counts["GTE"]);
        Assert.Equal(10, counts["LT"]);
        Assert.Equal(10, counts["LTE"]);
        Assert.Equal(10, counts["EQ"]);
        Assert.Equal(10, counts["NEQ"]);
        Assert.Equal(12, counts["REJECT"]);
        Assert.Equal(7, counts.Count);
    }

    [Fact]
    public void Development_HasManifestedClassAndRejectionCounts()
    {
        IReadOnlyDictionary<string, int> counts =
            ComparisonBenchmarkManifestVerifier.CountDispositions(LoadDevelopment());

        Assert.Equal(
            new[] { "EQ:6", "GT:8", "GTE:10", "LT:8", "LTE:10", "NEQ:6", "REJECT:12" },
            counts.Select(pair => $"{pair.Key}:{pair.Value}"));
    }

    [Fact]
    public void Manifest_CountsHashesRolesAndFreezePolicy_Verify()
    {
        string benchmarkDirectory = BenchmarkDirectory;

        IReadOnlyList<BenchmarkValidationIssue> issues = ComparisonBenchmarkManifestVerifier.Verify(
            Path.Combine(benchmarkDirectory, "benchmark_manifest.json"),
            benchmarkDirectory,
            LoadDevelopment(),
            LoadGate());

        Assert.Empty(issues);
        Assert.Equal(
            SealedGateHash,
            ComparisonBenchmarkManifestVerifier.ComputeCanonicalSha256(
                Path.Combine(benchmarkDirectory, "comparison_gate_sealed.jsonl")));
    }

    [Fact]
    public void EverySemanticExpectedValue_UsesP01JsonAndValidationContracts()
    {
        IEnumerable<ComparisonBenchmarkCase> semanticCases = LoadDevelopment().Cases
            .Concat(LoadGate().Cases)
            .Where(item => item.Expectation == ComparisonBenchmarkExpectation.Semantic);

        foreach (ComparisonBenchmarkCase benchmarkCase in semanticCases)
        {
            Comparison expected = Assert.IsType<Comparison>(benchmarkCase.Expected);
            string json = SemanticJson.Serialize(expected);

            Assert.True(
                SemanticJson.TryDeserialize(json, out SemanticExpression? roundTripped, out var jsonIssues),
                $"{benchmarkCase.Id}: {string.Join("; ", jsonIssues.Select(issue => issue.Code))}");
            Assert.Equal(expected, roundTripped);
            Assert.Empty(SemanticValidator.Validate(expected));
        }
    }

    [Fact]
    public void SuppliedCases_HaveUniqueIdsAndRequiredNonblankStrings()
    {
        ComparisonBenchmarkCase[] cases = LoadDevelopment().Cases.Concat(LoadGate().Cases).ToArray();

        Assert.Equal(cases.Length, cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(cases, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Id));
            Assert.Equal("en", item.Language);
            Assert.False(string.IsNullOrWhiteSpace(item.Input));
            Assert.NotEmpty(item.Tags);
            Assert.All(item.Tags, tag => Assert.False(string.IsNullOrWhiteSpace(tag)));
        });
    }

    [Fact]
    public void SuppliedDevelopmentAndGate_HaveNoExactOrTemplateLeakage()
    {
        IReadOnlyList<BenchmarkLeakIssue> issues = ComparisonBenchmarkLeakAnalyzer.Analyze(
            LoadDevelopment(),
            LoadGate());

        Assert.Empty(issues);
    }

    [Fact]
    public void CanonicalHash_IsIndependentOfBomLineEndingsAndTerminalNewlines()
    {
        const string lf = "first\nsecond\n";
        const string crlfWithExtraTerminalLines = "\uFEFFfirst\r\nsecond\r\n\r\n";
        const string bareCrWithoutTerminalLine = "first\rsecond";

        string expected = ComparisonBenchmarkManifestVerifier.ComputeCanonicalSha256Text(lf);

        Assert.Equal(expected, ComparisonBenchmarkManifestVerifier.ComputeCanonicalSha256Text(crlfWithExtraTerminalLines));
        Assert.Equal(expected, ComparisonBenchmarkManifestVerifier.ComputeCanonicalSha256Text(bareCrWithoutTerminalLine));
    }

    private static ComparisonBenchmarkSplit LoadDevelopment() =>
        ComparisonBenchmarkLoader.LoadFile(Path.Combine(BenchmarkDirectory, "comparison_development.jsonl"));

    private static ComparisonBenchmarkSplit LoadGate() =>
        ComparisonBenchmarkLoader.LoadFile(Path.Combine(BenchmarkDirectory, "comparison_gate_sealed.jsonl"));

    private static string BenchmarkDirectory => Path.Combine(AppContext.BaseDirectory, "BenchmarkData");
}
