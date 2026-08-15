using System.Text;
using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Source;
using Vector.Core.Syntax.Expressions;
using Vector.Core.Syntax.Statements;
using Xunit;

namespace Vector.Tests.Parsing;

public sealed class StatementParserTests
{
    [Fact]
    public void Parser_ParsesEmptyCompilationUnit()
    {
        var result = Parse(string.Empty);

        Assert.Empty(result.Root.Statements);
        Assert.Equal(0, result.Root.Span.Length);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesLetDeclarationWithRequiredInitializer()
    {
        var result = Parse("let answer = 42;");

        var declaration = Assert.IsType<VariableDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Equal("answer", declaration.Name);
        Assert.Equal(42d, Assert.IsType<double>(Assert.IsType<LiteralExpression>(declaration.Initializer).Value));
        Assert.Equal("let answer = 42;".Length, declaration.Span.Length);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_UsesNormalizedUnicodeIdentifierForDeclarationName()
    {
        var sourceName = "cafe\u0301";
        var result = Parse($"let {sourceName} = 1;");

        var declaration = Assert.IsType<VariableDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Equal(sourceName.Normalize(NormalizationForm.FormC), declaration.Name);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesExpressionStatement()
    {
        var result = Parse("1 + 2 * 3;");

        var statement = Assert.IsType<ExpressionStatement>(Assert.Single(result.Root.Statements));
        var add = Assert.IsType<BinaryExpression>(statement.Expression);
        Assert.Equal(Vector.Core.Lexing.TokenKind.Plus, add.OperatorToken.Kind);
        Assert.IsType<BinaryExpression>(add.Right);
        Assert.Equal("1 + 2 * 3;".Length, statement.Span.Length);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesAssignmentAsExpressionStatement()
    {
        var result = Parse("value = 20;");

        var statement = Assert.IsType<ExpressionStatement>(Assert.Single(result.Root.Statements));
        var assignment = Assert.IsType<AssignmentExpression>(statement.Expression);
        Assert.Equal("value", Assert.IsType<NameExpression>(assignment.Target).Name);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesMultipleTopLevelStatementsInSourceOrder()
    {
        var result = Parse("let x = 1; x = x + 1; x;");

        Assert.Collection(
            result.Root.Statements,
            statement => Assert.IsType<VariableDeclaration>(statement),
            statement => Assert.IsType<ExpressionStatement>(statement),
            statement => Assert.IsType<ExpressionStatement>(statement));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesBraceDelimitedBlock()
    {
        var result = Parse("{ let x = 1; x; }");

        var block = Assert.IsType<BlockStatement>(Assert.Single(result.Root.Statements));
        Assert.Collection(
            block.Statements,
            statement => Assert.IsType<VariableDeclaration>(statement),
            statement => Assert.IsType<ExpressionStatement>(statement));
        Assert.Equal("{ let x = 1; x; }".Length, block.Span.Length);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesNestedBlocks()
    {
        var result = Parse("{ { let x = 1; } }");

        var outer = Assert.IsType<BlockStatement>(Assert.Single(result.Root.Statements));
        var inner = Assert.IsType<BlockStatement>(Assert.Single(outer.Statements));
        Assert.IsType<VariableDeclaration>(Assert.Single(inner.Statements));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesIfWithoutParenthesesOrElse()
    {
        var result = Parse("if score >= 90 { score; }");

        var statement = Assert.IsType<IfStatement>(Assert.Single(result.Root.Statements));
        Assert.IsType<BinaryExpression>(statement.Condition);
        Assert.Single(statement.ThenBranch.Statements);
        Assert.Null(statement.ElseBranch);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesIfElse()
    {
        var result = Parse("if ready { 1; } else { 2; }");

        var statement = Assert.IsType<IfStatement>(Assert.Single(result.Root.Statements));
        Assert.IsType<BlockStatement>(statement.ElseBranch);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesElseIfAsNestedIfStatement()
    {
        var result = Parse("if first { 1; } else if second { 2; } else { 3; }");

        var first = Assert.IsType<IfStatement>(Assert.Single(result.Root.Statements));
        var second = Assert.IsType<IfStatement>(first.ElseBranch);
        Assert.IsType<BlockStatement>(second.ElseBranch);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_AllowsParenthesizedIfConditionAsOrdinaryGrouping()
    {
        var result = Parse("if (score >= 80 and score < 90) { score; }");

        var statement = Assert.IsType<IfStatement>(Assert.Single(result.Root.Statements));
        Assert.IsType<GroupingExpression>(statement.Condition);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_RequiresSemicolonAfterLetDeclaration()
    {
        var result = Parse("let x = 1");

        Assert.IsType<VariableDeclaration>(Assert.Single(result.Root.Statements));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.UnexpectedToken, diagnostic.Code);
        Assert.Contains("';'", diagnostic.Message);
    }

    [Fact]
    public void Parser_RequiresSemicolonAfterExpressionStatement()
    {
        var result = Parse("x + 1");

        Assert.IsType<ExpressionStatement>(Assert.Single(result.Root.Statements));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.UnexpectedToken, diagnostic.Code);
        Assert.Contains("';'", diagnostic.Message);
    }

    [Fact]
    public void Parser_DoesNotRequireSemicolonAfterBlockStatement()
    {
        var result = Parse("{ 1; } let x = 2;");

        Assert.Collection(
            result.Root.Statements,
            statement => Assert.IsType<BlockStatement>(statement),
            statement => Assert.IsType<VariableDeclaration>(statement));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ReportsMissingClosingBraceAtEndOfFile()
    {
        var result = Parse("{ let x = 1;");

        Assert.IsType<BlockStatement>(Assert.Single(result.Root.Statements));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.UnexpectedToken, diagnostic.Code);
        Assert.Contains("'}'", diagnostic.Message);
    }

    [Fact]
    public void Parser_ReportsMissingThenBlockWithoutConsumingFollowingStatement()
    {
        var result = Parse("if ready let x = 1;");

        Assert.Equal(2, result.Root.Statements.Count);
        var conditional = Assert.IsType<IfStatement>(result.Root.Statements[0]);
        Assert.Empty(conditional.ThenBranch.Statements);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("'{'"));
    }

    [Fact]
    public void Parser_ReportsInvalidElseBranchAndContinuesWithFollowingStatement()
    {
        var result = Parse("if ready { 1; } else let x = 2;");

        Assert.Equal(2, result.Root.Statements.Count);
        var conditional = Assert.IsType<IfStatement>(result.Root.Statements[0]);
        Assert.IsType<BlockStatement>(conditional.ElseBranch);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("'if' or '{'"));
    }

    [Fact]
    public void Parser_TracksStatementAndCompilationUnitSpansAcrossLines()
    {
        var source = "let x = 1;\r\nif x {\r\n    x;\r\n}";
        var result = Parse(source);

        var declaration = Assert.IsType<VariableDeclaration>(result.Root.Statements[0]);
        var conditional = Assert.IsType<IfStatement>(result.Root.Statements[1]);

        Assert.Equal(new SourcePosition(0, 1, 1), declaration.Span.Start);
        Assert.Equal(new SourcePosition(10, 1, 11), declaration.Span.End);
        Assert.Equal(new SourcePosition(12, 2, 1), conditional.Span.Start);
        Assert.Equal(source.Length, conditional.Span.End.Offset);
        Assert.Equal(new SourcePosition(0, 1, 1), result.Root.Span.Start);
        Assert.Equal(source.Length, result.Root.Span.End.Offset);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_PreservesLexerDiagnosticsWhenParsingCompilationUnit()
    {
        var result = Parse("let x = 1; @ let y = 2;");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.InvalidCharacter);
        Assert.Equal(2, result.Root.Statements.OfType<VariableDeclaration>().Count());
    }

    [Fact]
    public void Parser_SkipsCommentsBetweenStatementsAndBranches()
    {
        var result = Parse("let x = 1; // first\nif x { /* body */ x; } else { 0; }");

        Assert.Equal(2, result.Root.Statements.Count);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[0]);
        Assert.IsType<IfStatement>(result.Root.Statements[1]);
        Assert.Empty(result.Diagnostics);
    }

    private static ParseResult<Vector.Core.Syntax.CompilationUnit> Parse(string source) =>
        new Parser(new SourceText(source)).ParseCompilationUnit();
}
