namespace Vector.Npl.Semantics.Tests.Benchmarking;

using System.Text;
using System.Text.RegularExpressions;
using Vector.Npl.Semantics;

internal static partial class ComparisonBenchmarkLeakAnalyzer
{
    public static IReadOnlyList<BenchmarkLeakIssue> Analyze(
        ComparisonBenchmarkSplit development,
        ComparisonBenchmarkSplit gate)
    {
        var issues = new List<BenchmarkLeakIssue>();
        var developmentIds = development.Cases.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var developmentInputs = development.Cases
            .GroupBy(item => ComparisonBenchmarkLoader.NormalizeInput(item.Input), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var developmentDispositions = development.Cases
            .GroupBy(DispositionKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var developmentTemplates = development.Cases
            .Where(item => item.Expectation == ComparisonBenchmarkExpectation.Semantic)
            .GroupBy(CreateSurfaceTemplate, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (ComparisonBenchmarkCase gateCase in gate.Cases)
        {
            if (developmentIds.TryGetValue(gateCase.Id, out ComparisonBenchmarkCase? idMatch))
            {
                issues.Add(new BenchmarkLeakIssue(
                    "CROSS_SPLIT_ID_DUPLICATE",
                    idMatch.Id,
                    gateCase.Id,
                    gateCase.Id));
            }

            string normalizedInput = ComparisonBenchmarkLoader.NormalizeInput(gateCase.Input);
            if (developmentInputs.TryGetValue(normalizedInput, out ComparisonBenchmarkCase? inputMatch))
            {
                issues.Add(new BenchmarkLeakIssue(
                    "CROSS_SPLIT_INPUT_DUPLICATE",
                    inputMatch.Id,
                    gateCase.Id,
                    normalizedInput));
            }

            string dispositionKey = DispositionKey(gateCase);
            if (developmentDispositions.TryGetValue(dispositionKey, out ComparisonBenchmarkCase? dispositionMatch))
            {
                issues.Add(new BenchmarkLeakIssue(
                    "CROSS_SPLIT_INPUT_DISPOSITION_DUPLICATE",
                    dispositionMatch.Id,
                    gateCase.Id,
                    dispositionKey));
            }

            if (gateCase.Expectation == ComparisonBenchmarkExpectation.Semantic)
            {
                string template = CreateSurfaceTemplate(gateCase);
                if (developmentTemplates.TryGetValue(template, out ComparisonBenchmarkCase? templateMatch))
                {
                    issues.Add(new BenchmarkLeakIssue(
                        "CROSS_SPLIT_TEMPLATE_OVERLAP",
                        templateMatch.Id,
                        gateCase.Id,
                        template));
                }
            }
        }

        return issues.AsReadOnly();
    }

    public static string CreateSurfaceTemplate(ComparisonBenchmarkCase benchmarkCase)
    {
        string template = benchmarkCase.Input.Normalize(NormalizationForm.FormKC);
        template = QuotedTextRegex().Replace(template, "<TEXT>");
        template = NumberRegex().Replace(template, "<NUMBER>");
        template = BooleanRegex().Replace(template, "<BOOLEAN>");

        if (benchmarkCase.Expected is Comparison comparison)
        {
            foreach (string identifier in EnumerateIdentifiers(comparison).OrderByDescending(value => value.Length))
            {
                string pattern = $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(identifier)}(?![\p{{L}}\p{{N}}_])";
                template = Regex.Replace(
                    template,
                    pattern,
                    "<ID>",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
        }

        return ComparisonBenchmarkLoader.NormalizeInput(template);
    }

    private static string DispositionKey(ComparisonBenchmarkCase benchmarkCase)
    {
        string disposition = benchmarkCase.Expectation == ComparisonBenchmarkExpectation.Semantic
            ? $"semantic:{SemanticJson.Serialize(benchmarkCase.Expected)}"
            : $"reject:{benchmarkCase.RejectReason}";
        return $"{ComparisonBenchmarkLoader.NormalizeInput(benchmarkCase.Input)}\u001F{disposition}";
    }

    private static IEnumerable<string> EnumerateIdentifiers(SemanticExpression expression)
    {
        switch (expression)
        {
            case Identifier identifier when identifier.Name is not null:
                yield return identifier.Name;
                break;

            case Comparison comparison:
                if (comparison.Left is not null)
                {
                    foreach (string name in EnumerateIdentifiers(comparison.Left))
                    {
                        yield return name;
                    }
                }

                if (comparison.Right is not null)
                {
                    foreach (string name in EnumerateIdentifiers(comparison.Right))
                    {
                        yield return name;
                    }
                }

                break;
        }
    }

    [GeneratedRegex("\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedTextRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}_])[-+]?(?:\d+(?:\.\d+)?|\.\d+)(?![\p{L}\p{N}_])", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\b(?:true|false)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BooleanRegex();
}
