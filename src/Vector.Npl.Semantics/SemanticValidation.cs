namespace Vector.Npl.Semantics;

public sealed record SemanticValidationIssue(string Code, string Path, string Message);

public static class SemanticValidator
{
    public static IReadOnlyList<SemanticValidationIssue> Validate(SemanticExpression? expression)
    {
        var issues = new List<SemanticValidationIssue>();
        ValidateExpression(expression, "$", issues, isRoot: true);
        return issues;
    }

    private static void ValidateExpression(
        SemanticExpression? expression,
        string path,
        ICollection<SemanticValidationIssue> issues,
        bool isRoot = false)
    {
        if (expression is null)
        {
            issues.Add(new SemanticValidationIssue(
                isRoot ? "SEMANTIC_ROOT_REQUIRED" : "SEMANTIC_EXPRESSION_REQUIRED",
                path,
                isRoot ? "A semantic root expression is required." : "A semantic expression is required."));
            return;
        }

        switch (expression)
        {
            case Comparison comparison:
                if (!Enum.IsDefined(comparison.Kind))
                {
                    issues.Add(new SemanticValidationIssue(
                        "COMPARISON_KIND_INVALID",
                        $"{path}.kind",
                        $"Comparison kind '{(int)comparison.Kind}' is not supported."));
                }

                ValidateOperand(comparison.Left, $"{path}.left", "COMPARISON_LEFT_REQUIRED", issues);
                ValidateOperand(comparison.Right, $"{path}.right", "COMPARISON_RIGHT_REQUIRED", issues);
                break;

            case Identifier identifier when string.IsNullOrWhiteSpace(identifier.Name):
                issues.Add(new SemanticValidationIssue(
                    "IDENTIFIER_NAME_REQUIRED",
                    $"{path}.name",
                    "An identifier name must contain at least one non-whitespace character."));
                break;

            case NumberLiteral number when !double.IsFinite(number.Value):
                issues.Add(new SemanticValidationIssue(
                    "NUMBER_NOT_FINITE",
                    $"{path}.value",
                    "A number literal must be finite."));
                break;

            case TextLiteral text when text.Value is null:
                issues.Add(new SemanticValidationIssue(
                    "TEXT_VALUE_REQUIRED",
                    $"{path}.value",
                    "A text literal value cannot be null."));
                break;

            case Identifier:
            case NumberLiteral:
            case TextLiteral:
            case BooleanLiteral:
            case CurrentItem:
                break;

            default:
                issues.Add(new SemanticValidationIssue(
                    "SEMANTIC_NODE_UNSUPPORTED",
                    path,
                    $"Semantic node type '{expression.GetType().FullName}' is not supported."));
                break;
        }
    }

    private static void ValidateOperand(
        SemanticExpression? operand,
        string path,
        string requiredCode,
        ICollection<SemanticValidationIssue> issues)
    {
        if (operand is null)
        {
            issues.Add(new SemanticValidationIssue(requiredCode, path, "A comparison operand is required."));
            return;
        }

        ValidateExpression(operand, path, issues);
    }
}

public sealed class SemanticValidationException : ArgumentException
{
    public SemanticValidationException(IReadOnlyList<SemanticValidationIssue> issues)
        : base("The semantic expression is invalid.")
    {
        Issues = issues;
    }

    public IReadOnlyList<SemanticValidationIssue> Issues { get; }
}
