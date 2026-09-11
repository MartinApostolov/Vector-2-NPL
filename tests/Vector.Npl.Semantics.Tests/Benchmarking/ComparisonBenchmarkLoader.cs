namespace Vector.Npl.Semantics.Tests.Benchmarking;

using System.Text;
using System.Text.Json;
using Vector.Npl.Semantics;

internal static class ComparisonBenchmarkLoader
{
    private static readonly HashSet<string> AllowedMembers = new(StringComparer.Ordinal)
    {
        "id",
        "language",
        "input",
        "expectation",
        "expected",
        "rejectReason",
        "tags",
        "pair",
        "notes",
    };

    public static ComparisonBenchmarkSplit LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            string text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
            return LoadText(text, Path.GetFileName(path));
        }
        catch (DecoderFallbackException exception)
        {
            throw new ComparisonBenchmarkException(
            [
                new BenchmarkValidationIssue("BENCHMARK_UTF8_INVALID", "$", exception.Message),
            ]);
        }
    }

    public static ComparisonBenchmarkSplit LoadText(string? text, string sourceName = "<memory>")
    {
        var issues = new List<BenchmarkValidationIssue>();
        var cases = new List<ComparisonBenchmarkCase>();

        if (text is null)
        {
            throw new ComparisonBenchmarkException(
            [
                new BenchmarkValidationIssue("BENCHMARK_TEXT_REQUIRED", "$", "Benchmark JSONL text is required."),
            ]);
        }

        string normalized = NormalizeLineEndingsAndBom(text);
        string content = normalized.EndsWith('\n') ? normalized[..^1] : normalized;
        if (content.Length == 0)
        {
            throw new ComparisonBenchmarkException(
            [
                new BenchmarkValidationIssue("BENCHMARK_EMPTY", "$", "A benchmark split must contain at least one case."),
            ]);
        }

        string[] lines = content.Split('\n');
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var normalizedInputs = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int index = 0; index < lines.Length; index++)
        {
            int lineNumber = index + 1;
            string linePath = $"$[{lineNumber}]";
            string line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                AddIssue(issues, "BENCHMARK_LINE_EMPTY", linePath, "Empty JSONL lines are not allowed.");
                continue;
            }

            int issueCountBeforeLine = issues.Count;
            ComparisonBenchmarkCase? benchmarkCase = ReadCase(line, linePath, issues);
            if (benchmarkCase is null || issues.Count != issueCountBeforeLine)
            {
                continue;
            }

            if (!ids.Add(benchmarkCase.Id))
            {
                AddIssue(
                    issues,
                    "BENCHMARK_ID_DUPLICATE",
                    $"{linePath}.id",
                    $"Case id '{benchmarkCase.Id}' appears more than once in the split.");
            }

            string normalizedInput = NormalizeInput(benchmarkCase.Input);
            if (normalizedInputs.TryGetValue(normalizedInput, out string? existingId))
            {
                AddIssue(
                    issues,
                    "BENCHMARK_INPUT_DUPLICATE",
                    $"{linePath}.input",
                    $"Input duplicates case '{existingId}' after normalization.");
            }
            else
            {
                normalizedInputs.Add(normalizedInput, benchmarkCase.Id);
            }

            cases.Add(benchmarkCase);
        }

        if (issues.Count != 0)
        {
            throw new ComparisonBenchmarkException(issues.AsReadOnly());
        }

        return new ComparisonBenchmarkSplit(sourceName, cases.AsReadOnly());
    }

    public static string NormalizeInput(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        string normalized = input.Normalize(NormalizationForm.FormKC).Trim();
        var result = new StringBuilder(normalized.Length);
        bool pendingSpace = false;

        foreach (char character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length != 0;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(character);
        }

        return result.ToString().ToUpperInvariant();
    }

    private static ComparisonBenchmarkCase? ReadCase(
        string line,
        string path,
        ICollection<BenchmarkValidationIssue> issues)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        }
        catch (JsonException exception)
        {
            AddIssue(issues, "BENCHMARK_JSON_MALFORMED", path, exception.Message);
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                AddIssue(issues, "BENCHMARK_CASE_OBJECT_REQUIRED", path, "Each JSONL line must be a JSON object.");
                return null;
            }

            Dictionary<string, JsonElement> properties = ReadProperties(document.RootElement, path, issues);
            RejectUnknownMembers(properties, path, issues);

            bool hasId = TryReadRequiredNonblankString(properties, "id", path, issues, out string? id);
            bool hasLanguage = TryReadRequiredNonblankString(properties, "language", path, issues, out string? language);
            if (hasLanguage && !string.Equals(language, "en", StringComparison.Ordinal))
            {
                AddIssue(issues, "BENCHMARK_LANGUAGE_UNSUPPORTED", $"{path}.language", "Comparison benchmark language must be 'en'.");
                hasLanguage = false;
            }

            bool hasInput = TryReadRequiredNonblankString(properties, "input", path, issues, out string? input);
            bool hasExpectation = TryReadRequiredNonblankString(
                properties,
                "expectation",
                path,
                issues,
                out string? expectationText);
            bool expectationSupported = TryParseExpectation(expectationText, path, issues, out ComparisonBenchmarkExpectation expectation);
            IReadOnlyList<string>? tags = ReadTags(properties, path, issues);
            string? pair = ReadOptionalString(properties, "pair", path, issues);
            string? notes = ReadOptionalString(properties, "notes", path, issues);

            SemanticExpression? expected = null;
            string? rejectReason = null;

            if (expectationSupported && expectation == ComparisonBenchmarkExpectation.Semantic)
            {
                expected = ReadExpected(properties, path, issues);
                if (properties.ContainsKey("rejectReason"))
                {
                    AddIssue(
                        issues,
                        "BENCHMARK_SEMANTIC_REJECT_REASON_FORBIDDEN",
                        $"{path}.rejectReason",
                        "A semantic case must not contain 'rejectReason'.");
                }
            }
            else if (expectationSupported && expectation == ComparisonBenchmarkExpectation.Reject)
            {
                if (properties.ContainsKey("expected"))
                {
                    AddIssue(
                        issues,
                        "BENCHMARK_REJECT_EXPECTED_FORBIDDEN",
                        $"{path}.expected",
                        "A rejection case must not contain 'expected'.");
                }

                TryReadRequiredNonblankString(properties, "rejectReason", path, issues, out rejectReason);
            }

            if (!hasId || !hasLanguage || !hasInput || !hasExpectation || !expectationSupported || tags is null)
            {
                return null;
            }

            return new ComparisonBenchmarkCase(
                id!,
                language!,
                input!,
                expectation,
                expected,
                rejectReason,
                tags,
                pair,
                notes);
        }
    }

    private static SemanticExpression? ReadExpected(
        IReadOnlyDictionary<string, JsonElement> properties,
        string path,
        ICollection<BenchmarkValidationIssue> issues)
    {
        if (!properties.TryGetValue("expected", out JsonElement expectedElement))
        {
            AddIssue(
                issues,
                "BENCHMARK_SEMANTIC_EXPECTED_REQUIRED",
                $"{path}.expected",
                "A semantic case requires an 'expected' Semantic IR value.");
            return null;
        }

        string expectedJson = expectedElement.GetRawText();
        if (!SemanticJson.TryDeserialize(expectedJson, out SemanticExpression? expected, out var semanticIssues))
        {
            foreach (SemanticValidationIssue issue in semanticIssues)
            {
                string suffix = issue.Path == "$" ? string.Empty : issue.Path[1..];
                AddIssue(
                    issues,
                    "BENCHMARK_EXPECTED_IR_INVALID",
                    $"{path}.expected{suffix}",
                    $"{issue.Code}: {issue.Message}");
            }

            return null;
        }

        IReadOnlyList<SemanticValidationIssue> validationIssues = SemanticValidator.Validate(expected);
        foreach (SemanticValidationIssue issue in validationIssues)
        {
            string suffix = issue.Path == "$" ? string.Empty : issue.Path[1..];
            AddIssue(
                issues,
                "BENCHMARK_EXPECTED_IR_INVALID",
                $"{path}.expected{suffix}",
                $"{issue.Code}: {issue.Message}");
        }

        if (expected is not Comparison)
        {
            AddIssue(
                issues,
                "BENCHMARK_EXPECTED_COMPARISON_REQUIRED",
                $"{path}.expected",
                "A semantic comparison benchmark case must expect a Comparison node.");
            return null;
        }

        return expected;
    }

    private static IReadOnlyList<string>? ReadTags(
        IReadOnlyDictionary<string, JsonElement> properties,
        string path,
        ICollection<BenchmarkValidationIssue> issues)
    {
        if (!properties.TryGetValue("tags", out JsonElement tagsElement))
        {
            AddIssue(issues, "BENCHMARK_TAGS_REQUIRED", $"{path}.tags", "The 'tags' member is required.");
            return null;
        }

        if (tagsElement.ValueKind != JsonValueKind.Array)
        {
            AddIssue(issues, "BENCHMARK_TAGS_ARRAY_REQUIRED", $"{path}.tags", "The 'tags' member must be an array.");
            return null;
        }

        var tags = new List<string>();
        int index = 0;
        foreach (JsonElement tagElement in tagsElement.EnumerateArray())
        {
            if (tagElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(tagElement.GetString()))
            {
                AddIssue(
                    issues,
                    "BENCHMARK_TAG_INVALID",
                    $"{path}.tags[{index}]",
                    "Every tag must be a nonblank string.");
            }
            else
            {
                tags.Add(tagElement.GetString()!);
            }

            index++;
        }

        if (index == 0)
        {
            AddIssue(issues, "BENCHMARK_TAGS_EMPTY", $"{path}.tags", "At least one tag is required.");
        }

        return tags.AsReadOnly();
    }

    private static bool TryParseExpectation(
        string? value,
        string path,
        ICollection<BenchmarkValidationIssue> issues,
        out ComparisonBenchmarkExpectation expectation)
    {
        expectation = default;
        if (value is null)
        {
            return false;
        }

        if (string.Equals(value, "semantic", StringComparison.Ordinal))
        {
            expectation = ComparisonBenchmarkExpectation.Semantic;
            return true;
        }

        if (string.Equals(value, "reject", StringComparison.Ordinal))
        {
            expectation = ComparisonBenchmarkExpectation.Reject;
            return true;
        }

        AddIssue(
            issues,
            "BENCHMARK_EXPECTATION_UNSUPPORTED",
            $"{path}.expectation",
            "The expectation must be exactly 'semantic' or 'reject'.");
        return false;
    }

    private static bool TryReadRequiredNonblankString(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name,
        string path,
        ICollection<BenchmarkValidationIssue> issues,
        out string? value)
    {
        value = null;
        if (!properties.TryGetValue(name, out JsonElement element))
        {
            AddIssue(issues, "BENCHMARK_MEMBER_REQUIRED", $"{path}.{name}", $"The '{name}' member is required.");
            return false;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            AddIssue(issues, "BENCHMARK_STRING_REQUIRED", $"{path}.{name}", $"The '{name}' member must be a string.");
            return false;
        }

        value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            AddIssue(issues, "BENCHMARK_STRING_BLANK", $"{path}.{name}", $"The '{name}' member must not be blank.");
            return false;
        }

        return true;
    }

    private static string? ReadOptionalString(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name,
        string path,
        ICollection<BenchmarkValidationIssue> issues)
    {
        if (!properties.TryGetValue(name, out JsonElement element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            AddIssue(
                issues,
                "BENCHMARK_OPTIONAL_STRING_INVALID",
                $"{path}.{name}",
                $"Optional member '{name}' must be a string when present.");
            return null;
        }

        return element.GetString();
    }

    private static Dictionary<string, JsonElement> ReadProperties(
        JsonElement element,
        string path,
        ICollection<BenchmarkValidationIssue> issues)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!properties.TryAdd(property.Name, property.Value))
            {
                AddIssue(
                    issues,
                    "BENCHMARK_MEMBER_DUPLICATE",
                    $"{path}.{property.Name}",
                    $"Member '{property.Name}' appears more than once.");
            }
        }

        return properties;
    }

    private static void RejectUnknownMembers(
        IReadOnlyDictionary<string, JsonElement> properties,
        string path,
        ICollection<BenchmarkValidationIssue> issues)
    {
        foreach (string name in properties.Keys)
        {
            if (!AllowedMembers.Contains(name))
            {
                AddIssue(
                    issues,
                    "BENCHMARK_MEMBER_UNKNOWN",
                    $"{path}.{name}",
                    $"Member '{name}' is not part of the comparison benchmark schema.");
            }
        }
    }

    private static string NormalizeLineEndingsAndBom(string text)
    {
        if (text.Length != 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static void AddIssue(
        ICollection<BenchmarkValidationIssue> issues,
        string code,
        string path,
        string message) => issues.Add(new BenchmarkValidationIssue(code, path, message));
}
