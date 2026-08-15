using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Source;
using Vector.Core.Syntax.Expressions;
using Vector.Core.Syntax.Statements;
using Xunit;

namespace Vector.Tests.Parsing;

public sealed class LoopParserTests
{
    [Fact]
    public void Parser_ParsesWhileStatement()
    {
        var result = Parse("while x < 10 { x = x + 1; }");

        var loop = Assert.IsType<WhileStatement>(Assert.Single(result.Root.Statements));
        Assert.IsType<BinaryExpression>(loop.Condition);
        Assert.Single(loop.Body.Statements);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_AllowsParenthesizedWhileConditionAsGrouping()
    {
        var result = Parse("while (ready and active) { continue; }");

        var loop = Assert.IsType<WhileStatement>(Assert.Single(result.Root.Statements));
        Assert.IsType<GroupingExpression>(loop.Condition);
        Assert.IsType<ContinueStatement>(Assert.Single(loop.Body.Statements));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesForInStatement()
    {
        var result = Parse("for item in items { item; }");

        var loop = Assert.IsType<ForStatement>(Assert.Single(result.Root.Statements));
        Assert.Equal("item", loop.VariableName);
        Assert.Equal("items", Assert.IsType<NameExpression>(loop.Iterable).Name);
        Assert.Single(loop.Body.Statements);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesForIterableAsFullExpression()
    {
        var result = Parse("for item in values[1] { item; }");

        var loop = Assert.IsType<ForStatement>(Assert.Single(result.Root.Statements));
        Assert.IsType<IndexExpression>(loop.Iterable);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesBreakInsideWhile()
    {
        var result = Parse("while true { break; }");

        var loop = Assert.IsType<WhileStatement>(Assert.Single(result.Root.Statements));
        Assert.IsType<BreakStatement>(Assert.Single(loop.Body.Statements));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesContinueInsideFor()
    {
        var result = Parse("for item in items { continue; }");

        var loop = Assert.IsType<ForStatement>(Assert.Single(result.Root.Statements));
        Assert.IsType<ContinueStatement>(Assert.Single(loop.Body.Statements));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_AllowsLoopControlInsideNestedConditionalWithinLoop()
    {
        var result = Parse("while true { if done { break; } else { continue; } }");

        var loop = Assert.IsType<WhileStatement>(Assert.Single(result.Root.Statements));
        var conditional = Assert.IsType<IfStatement>(Assert.Single(loop.Body.Statements));
        Assert.IsType<BreakStatement>(Assert.Single(conditional.ThenBranch.Statements));
        var elseBlock = Assert.IsType<BlockStatement>(conditional.ElseBranch);
        Assert.IsType<ContinueStatement>(Assert.Single(elseBlock.Statements));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_AllowsNestedLoops()
    {
        var result = Parse("while outer { for item in items { break; } continue; }");

        var outer = Assert.IsType<WhileStatement>(Assert.Single(result.Root.Statements));
        Assert.Collection(
            outer.Body.Statements,
            statement => Assert.IsType<ForStatement>(statement),
            statement => Assert.IsType<ContinueStatement>(statement));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ReportsBreakOutsideLoop()
    {
        var result = Parse("break;");

        Assert.IsType<BreakStatement>(Assert.Single(result.Root.Statements));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidLoopControl, diagnostic.Code);
        Assert.Contains("inside a loop", diagnostic.Message);
    }

    [Fact]
    public void Parser_ReportsContinueOutsideLoop()
    {
        var result = Parse("continue;");

        Assert.IsType<ContinueStatement>(Assert.Single(result.Root.Statements));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidLoopControl, diagnostic.Code);
        Assert.Contains("inside a loop", diagnostic.Message);
    }

    [Fact]
    public void Parser_RequiresSemicolonAfterBreak()
    {
        var result = Parse("while true { break }");

        var loop = Assert.IsType<WhileStatement>(Assert.Single(result.Root.Statements));
        Assert.IsType<BreakStatement>(Assert.Single(loop.Body.Statements));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("';'"));
    }

    [Fact]
    public void Parser_RequiresSemicolonAfterContinue()
    {
        var result = Parse("while true { continue }");

        var loop = Assert.IsType<WhileStatement>(Assert.Single(result.Root.Statements));
        Assert.IsType<ContinueStatement>(Assert.Single(loop.Body.Statements));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("';'"));
    }

    [Fact]
    public void Parser_ReportsMissingForVariableName()
    {
        var result = Parse("for in items { break; }");

        var loop = Assert.IsType<ForStatement>(Assert.Single(result.Root.Statements));
        Assert.Equal("<missing>", loop.VariableName);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("identifier"));
    }

    [Fact]
    public void Parser_ReportsMissingInKeyword()
    {
        var result = Parse("for item items { break; }");

        Assert.IsType<ForStatement>(Assert.Single(result.Root.Statements));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("'in'"));
    }

    [Fact]
    public void Parser_ReportsMissingWhileBodyWithoutConsumingFollowingStatement()
    {
        var result = Parse("while ready let x = 1;");

        Assert.Equal(2, result.Root.Statements.Count);
        var loop = Assert.IsType<WhileStatement>(result.Root.Statements[0]);
        Assert.Empty(loop.Body.Statements);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("'{'"));
    }

    [Fact]
    public void Parser_ReportsMissingForBodyWithoutConsumingFollowingStatement()
    {
        var result = Parse("for item in items let x = 1;");

        Assert.Equal(2, result.Root.Statements.Count);
        var loop = Assert.IsType<ForStatement>(result.Root.Statements[0]);
        Assert.Empty(loop.Body.Statements);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("'{'"));
    }

    [Fact]
    public void Parser_TracksLoopStatementSpans()
    {
        var source = "while ready {\r\n    break;\r\n}";
        var result = Parse(source);

        var loop = Assert.IsType<WhileStatement>(Assert.Single(result.Root.Statements));
        var breakStatement = Assert.IsType<BreakStatement>(Assert.Single(loop.Body.Statements));

        Assert.Equal(new SourcePosition(0, 1, 1), loop.Span.Start);
        Assert.Equal(source.Length, loop.Span.End.Offset);
        Assert.Equal(new SourcePosition(19, 2, 5), breakStatement.Span.Start);
        Assert.Equal(new SourcePosition(25, 2, 11), breakStatement.Span.End);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_SkipsCommentsAroundLoopSyntax()
    {
        var result = Parse("while ready /* condition */ { // body\n break; } for item in items { continue; }");

        Assert.Collection(
            result.Root.Statements,
            statement => Assert.IsType<WhileStatement>(statement),
            statement => Assert.IsType<ForStatement>(statement));
        Assert.Empty(result.Diagnostics);
    }

    private static ParseResult<Vector.Core.Syntax.CompilationUnit> Parse(string source) =>
        new Parser(new SourceText(source)).ParseCompilationUnit();
}
