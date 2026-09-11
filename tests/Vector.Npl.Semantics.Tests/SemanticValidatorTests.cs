namespace Vector.Npl.Semantics.Tests;

using Vector.Npl.Semantics;
using Xunit;

public sealed class SemanticValidatorTests
{
    [Fact]
    public void NullRoot_IsInvalid()
    {
        SemanticValidationIssue issue = Assert.Single(SemanticValidator.Validate(null));

        Assert.Equal("SEMANTIC_ROOT_REQUIRED", issue.Code);
        Assert.Equal("$", issue.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingOrWhitespaceIdentifier_IsInvalid(string? name)
    {
        SemanticValidationIssue issue = Assert.Single(SemanticValidator.Validate(new Identifier(name)));

        Assert.Equal("IDENTIFIER_NAME_REQUIRED", issue.Code);
        Assert.Equal("$.name", issue.Path);
    }

    [Fact]
    public void NullText_IsInvalid()
    {
        SemanticValidationIssue issue = Assert.Single(SemanticValidator.Validate(new TextLiteral(null)));

        Assert.Equal("TEXT_VALUE_REQUIRED", issue.Code);
        Assert.Equal("$.value", issue.Path);
    }

    [Fact]
    public void EmptyText_IsValid()
    {
        Assert.Empty(SemanticValidator.Validate(new TextLiteral(string.Empty)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteNumber_IsInvalid(double value)
    {
        SemanticValidationIssue issue = Assert.Single(SemanticValidator.Validate(new NumberLiteral(value)));

        Assert.Equal("NUMBER_NOT_FINITE", issue.Code);
        Assert.Equal("$.value", issue.Path);
    }

    [Fact]
    public void NullComparisonOperands_AreInvalid()
    {
        var comparison = new Comparison(ComparisonKind.EQ, null, null);

        IReadOnlyList<SemanticValidationIssue> issues = SemanticValidator.Validate(comparison);

        Assert.Collection(
            issues,
            issue =>
            {
                Assert.Equal("COMPARISON_LEFT_REQUIRED", issue.Code);
                Assert.Equal("$.left", issue.Path);
            },
            issue =>
            {
                Assert.Equal("COMPARISON_RIGHT_REQUIRED", issue.Code);
                Assert.Equal("$.right", issue.Path);
            });
    }

    [Fact]
    public void InvalidComparisonEnum_IsInvalid()
    {
        var comparison = new Comparison(
            (ComparisonKind)999,
            new CurrentItem(),
            new NumberLiteral(10));

        SemanticValidationIssue issue = Assert.Single(SemanticValidator.Validate(comparison));

        Assert.Equal("COMPARISON_KIND_INVALID", issue.Code);
        Assert.Equal("$.kind", issue.Path);
    }

    [Fact]
    public void UnsupportedNodeImplementation_IsInvalid()
    {
        SemanticValidationIssue issue = Assert.Single(SemanticValidator.Validate(new UnsupportedExpression()));

        Assert.Equal("SEMANTIC_NODE_UNSUPPORTED", issue.Code);
        Assert.Equal("$", issue.Path);
    }

    [Fact]
    public void SupportedValidTree_HasNoIssues()
    {
        var expression = new Comparison(
            ComparisonKind.GTE,
            new Identifier("score"),
            new NumberLiteral(10));

        Assert.Empty(SemanticValidator.Validate(expression));
    }

    [Fact]
    public void CurrentItem_IsValid()
    {
        Assert.Empty(SemanticValidator.Validate(new CurrentItem()));
    }

    private sealed record UnsupportedExpression : SemanticExpression;
}
