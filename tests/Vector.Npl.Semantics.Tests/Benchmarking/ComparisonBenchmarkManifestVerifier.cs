namespace Vector.Npl.Semantics.Tests.Benchmarking;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vector.Npl.Semantics;

internal static class ComparisonBenchmarkManifestVerifier
{
    private const string DevelopmentFileName = "comparison_development.jsonl";
    private const string GateFileName = "comparison_gate_sealed.jsonl";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        AllowTrailingCommas = false,
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static IReadOnlyList<BenchmarkValidationIssue> Verify(
        string manifestPath,
        string benchmarkDirectory,
        ComparisonBenchmarkSplit development,
        ComparisonBenchmarkSplit gate)
    {
        var issues = new List<BenchmarkValidationIssue>();
        BenchmarkManifest? manifest;

        try
        {
            manifest = JsonSerializer.Deserialize<BenchmarkManifest>(
                File.ReadAllText(manifestPath, new UTF8Encoding(false, true)),
                ManifestJsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            issues.Add(new BenchmarkValidationIssue("MANIFEST_INVALID", "$", exception.Message));
            return issues.AsReadOnly();
        }

        if (manifest is null)
        {
            issues.Add(new BenchmarkValidationIssue("MANIFEST_REQUIRED", "$", "The benchmark manifest is required."));
            return issues.AsReadOnly();
        }

        RequireEqual(1, manifest.SchemaVersion, "$.schemaVersion", "MANIFEST_SCHEMA_VERSION_INVALID", issues);
        RequireEqual(
            "vector-npl-comparison-proof",
            manifest.Benchmark,
            "$.benchmark",
            "MANIFEST_BENCHMARK_INVALID",
            issues);
        RequireEqual("en", manifest.Language, "$.language", "MANIFEST_LANGUAGE_INVALID", issues);

        if (manifest.Files.Count != 2)
        {
            issues.Add(new BenchmarkValidationIssue(
                "MANIFEST_FILES_INVALID",
                "$.files",
                "The manifest must describe exactly the development and sealed-gate files."));
        }

        VerifyFile(
            manifest,
            DevelopmentFileName,
            "development",
            Path.Combine(benchmarkDirectory, DevelopmentFileName),
            development,
            issues);
        VerifyFile(
            manifest,
            GateFileName,
            "sealed-gate",
            Path.Combine(benchmarkDirectory, GateFileName),
            gate,
            issues);

        RequireEqual(true, manifest.Policy.DevelopmentMayInfluenceExperiments, "$.policy.developmentMayInfluenceExperiments", "MANIFEST_POLICY_INVALID", issues);
        RequireEqual(false, manifest.Policy.SealedGateMayInfluenceExperimentsBeforeP07, "$.policy.sealedGateMayInfluenceExperimentsBeforeP07", "MANIFEST_POLICY_INVALID", issues);
        RequireEqual(true, manifest.Policy.FailedSealedGateBecomesDevelopmentEvidence, "$.policy.failedSealedGateBecomesDevelopmentEvidence", "MANIFEST_POLICY_INVALID", issues);
        RequireEqual(true, manifest.Policy.NewSealedGateRequiredAfterGateDrivenChanges, "$.policy.newSealedGateRequiredAfterGateDrivenChanges", "MANIFEST_POLICY_INVALID", issues);

        RequireEqual("SHA-256", manifest.HashContract.Algorithm, "$.hashContract.algorithm", "MANIFEST_HASH_CONTRACT_INVALID", issues);
        RequireEqual("UTF-8 without BOM", manifest.HashContract.Encoding, "$.hashContract.encoding", "MANIFEST_HASH_CONTRACT_INVALID", issues);
        RequireEqual("Normalize CRLF and CR to LF", manifest.HashContract.LineEndings, "$.hashContract.lineEndings", "MANIFEST_HASH_CONTRACT_INVALID", issues);
        RequireEqual("Exactly one LF", manifest.HashContract.TerminalNewline, "$.hashContract.terminalNewline", "MANIFEST_HASH_CONTRACT_INVALID", issues);

        return issues.AsReadOnly();
    }

    public static string ComputeCanonicalSha256(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        string text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(bytes);
        return ComputeCanonicalSha256Text(text);
    }

    public static string ComputeCanonicalSha256Text(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length != 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        string canonical = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd('\n') + "\n";
        byte[] canonicalBytes = new UTF8Encoding(false).GetBytes(canonical);
        return Convert.ToHexString(SHA256.HashData(canonicalBytes)).ToLowerInvariant();
    }

    public static IReadOnlyDictionary<string, int> CountDispositions(ComparisonBenchmarkSplit split)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (ComparisonBenchmarkCase benchmarkCase in split.Cases)
        {
            string key = benchmarkCase.Expectation == ComparisonBenchmarkExpectation.Reject
                ? "REJECT"
                : ((Comparison)benchmarkCase.Expected!).Kind.ToString();
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        return counts;
    }

    private static void VerifyFile(
        BenchmarkManifest manifest,
        string fileName,
        string expectedRole,
        string path,
        ComparisonBenchmarkSplit split,
        ICollection<BenchmarkValidationIssue> issues)
    {
        string manifestPath = $"$.files.{fileName}";
        if (!manifest.Files.TryGetValue(fileName, out ManifestFile? file))
        {
            issues.Add(new BenchmarkValidationIssue(
                "MANIFEST_FILE_REQUIRED",
                manifestPath,
                $"The manifest entry for '{fileName}' is required."));
            return;
        }

        RequireEqual(expectedRole, file.Role, $"{manifestPath}.role", "MANIFEST_ROLE_MISMATCH", issues);
        RequireEqual(split.Cases.Count, file.CaseCount, $"{manifestPath}.caseCount", "MANIFEST_CASE_COUNT_MISMATCH", issues);

        IReadOnlyDictionary<string, int> actualCounts = CountDispositions(split);
        if (file.Counts.Count != actualCounts.Count ||
            file.Counts.Any(pair => !actualCounts.TryGetValue(pair.Key, out int count) || count != pair.Value))
        {
            issues.Add(new BenchmarkValidationIssue(
                "MANIFEST_CLASS_COUNTS_MISMATCH",
                $"{manifestPath}.counts",
                "Manifest class/disposition counts do not match the benchmark cases."));
        }

        try
        {
            string actualHash = ComputeCanonicalSha256(path);
            RequireEqual(file.Sha256, actualHash, $"{manifestPath}.sha256", "MANIFEST_HASH_MISMATCH", issues);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            issues.Add(new BenchmarkValidationIssue("MANIFEST_HASH_READ_FAILED", manifestPath, exception.Message));
        }
    }

    private static void RequireEqual<T>(
        T expected,
        T actual,
        string path,
        string code,
        ICollection<BenchmarkValidationIssue> issues)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            issues.Add(new BenchmarkValidationIssue(
                code,
                path,
                $"Expected '{expected}', but found '{actual}'."));
        }
    }

    private sealed class BenchmarkManifest
    {
        public required int SchemaVersion { get; init; }

        public required string Benchmark { get; init; }

        public required string Language { get; init; }

        public required Dictionary<string, ManifestFile> Files { get; init; }

        public required ManifestPolicy Policy { get; init; }

        public required ManifestHashContract HashContract { get; init; }
    }

    private sealed class ManifestFile
    {
        public required string Role { get; init; }

        public required int CaseCount { get; init; }

        public required Dictionary<string, int> Counts { get; init; }

        public required string Sha256 { get; init; }
    }

    private sealed class ManifestPolicy
    {
        public required bool DevelopmentMayInfluenceExperiments { get; init; }

        public required bool SealedGateMayInfluenceExperimentsBeforeP07 { get; init; }

        public required bool FailedSealedGateBecomesDevelopmentEvidence { get; init; }

        public required bool NewSealedGateRequiredAfterGateDrivenChanges { get; init; }
    }

    private sealed class ManifestHashContract
    {
        public required string Algorithm { get; init; }

        public required string Encoding { get; init; }

        public required string LineEndings { get; init; }

        public required string TerminalNewline { get; init; }
    }
}
