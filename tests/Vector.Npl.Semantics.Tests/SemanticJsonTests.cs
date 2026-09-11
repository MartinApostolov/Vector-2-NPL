namespace Vector.Npl.Semantics.Tests;

using Vector.Npl.Semantics;
using Xunit;

public sealed class SemanticJsonTests
{
    [Fact]
    public void ComparisonKind_ContainsExactlyTheInitialSemanticKinds()
    {
        Assert.Equal(
            ["GT", "GTE", "LT", "LTE", "EQ", "NEQ"],
            Enum.GetNames<ComparisonKind>());
    }

    [Fact]
    public void GteComparison_RoundTripsWithSemanticJsonContract()
    {
        var expected = new Comparison(
            ComparisonKind.GTE,
            new Identifier("score"),
            new NumberLiteral(10));

        string json = SemanticJson.Serialize(expected);
        bool success = SemanticJson.TryDeserialize(json, out SemanticExpression? actual, out var issues);

        Assert.True(success);
        Assert.Empty(issues);
        Assert.Equal(expected, actual);
        Assert.Contains("\"type\": \"comparison\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"GTE\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain(">=", json, StringComparison.Ordinal);
    }

    public static TheoryData<SemanticExpression> InitialExpressions => new()
    {
        new Comparison(ComparisonKind.EQ, new Identifier("left"), new Identifier("right")),
        new Identifier("score"),
        new NumberLiteral(10.5),
        new TextLiteral("hello"),
        new BooleanLiteral(true),
        new CurrentItem(),
    };

    public static TheoryData<SemanticExpression> InitialOperandExpressions => new()
    {
        new Identifier("score"),
        new NumberLiteral(10.5),
        new TextLiteral("hello"),
        new BooleanLiteral(true),
        new CurrentItem(),
    };

    [Theory]
    [MemberData(nameof(InitialExpressions))]
    public void EveryInitialExpressionType_RoundTrips(SemanticExpression expected)
    {
        string json = SemanticJson.Serialize(expected);

        SemanticExpression actual = SemanticJson.Deserialize(json);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(InitialOperandExpressions))]
    public void EveryInitialOperandType_RoundTripsInsideComparison(SemanticExpression operand)
    {
        var expected = new Comparison(ComparisonKind.EQ, operand, new CurrentItem());

        SemanticExpression actual = SemanticJson.Deserialize(SemanticJson.Serialize(expected));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("{\"type\":\"futureNode\"}", "JSON_NODE_TYPE_UNKNOWN")]
    [InlineData("{\"type\":\"currentItem\",\"extra\":true}", "JSON_MEMBER_UNKNOWN")]
    [InlineData("{\"type\":\"comparison\",\"kind\":1,\"left\":{\"type\":\"currentItem\"},\"right\":{\"type\":\"numberLiteral\",\"value\":10}}", "JSON_STRING_REQUIRED")]
    [InlineData("{\"type\":\"numberLiteral\",\"value\":\"10\"}", "JSON_NUMBER_REQUIRED")]
    [InlineData("{\"type\":\"booleanLiteral\",\"value\":\"true\"}", "JSON_BOOLEAN_REQUIRED")]
    [InlineData("{\"type\":\"identifier\",\"name\":\"score\",\"name\":\"other\"}", "JSON_MEMBER_DUPLICATE")]
    [InlineData("{\"type\":\"currentItem\",}", "JSON_MALFORMED")]
    [InlineData("not json", "JSON_MALFORMED")]
    public void InvalidJson_IsRejectedWithMachineReadableIssue(string json, string expectedCode)
    {
        bool success = SemanticJson.TryDeserialize(json, out SemanticExpression? expression, out var issues);

        Assert.False(success);
        Assert.Null(expression);
        Assert.Contains(issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void UnknownNestedProperty_IsRejectedAtItsPath()
    {
        const string json = """
            {
              "type": "comparison",
              "kind": "GTE",
              "left": { "type": "identifier", "name": "score", "extra": 1 },
              "right": { "type": "numberLiteral", "value": 10 }
            }
            """;

        Assert.False(SemanticJson.TryDeserialize(json, out _, out var issues));

        SemanticValidationIssue issue = Assert.Single(issues, issue => issue.Code == "JSON_MEMBER_UNKNOWN");
        Assert.Equal("$.left.extra", issue.Path);
    }

    [Fact]
    public void CurrentItem_HasNoPayloadAndRoundTrips()
    {
        string json = SemanticJson.Serialize(new CurrentItem());

        var result = Assert.IsType<CurrentItem>(SemanticJson.Deserialize(json));

        Assert.Equal(new CurrentItem(), result);
        Assert.Equal("{\r\n  \"type\": \"currentItem\"\r\n}", json.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
    }

    [Fact]
    public void Serialize_InvalidExpression_ThrowsWithValidationIssues()
    {
        var expression = new Identifier("  ");

        SemanticValidationException exception = Assert.Throws<SemanticValidationException>(
            () => SemanticJson.Serialize(expression));

        Assert.Contains(exception.Issues, issue => issue.Code == "IDENTIFIER_NAME_REQUIRED");
    }
}
