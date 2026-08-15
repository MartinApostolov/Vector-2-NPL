using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Source;
using Vector.Core.Syntax;
using Vector.Core.Syntax.Statements;
using Xunit;

namespace Vector.Tests.Parsing;

public sealed class ParserDiagnosticTests
{
    [Fact]
    public void Parser_MissingInitializerPreservesFollowingDeclaration()
    {
        var result = Parse("let x = ; let y = 2;");

        Assert.Collection(
            result.Root.Statements,
            statement => Assert.IsType<VariableDeclaration>(statement),
            statement => Assert.IsType<VariableDeclaration>(statement));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.ExpectedExpression);
    }

    [Fact]
    public void Parser_MissingBinaryOperandPreservesFollowingDeclaration()
    {
        var result = Parse("let x = 1 + ; let y = 2;");

        Assert.Equal(2, result.Root.Statements.Count);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[0]);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.ExpectedExpression);
    }

    [Fact]
    public void Parser_MissingSemicolonBeforeDeclarationReportsContextAndContinues()
    {
        var result = Parse("let x = 1 let y = 2;");

        Assert.Equal(2, result.Root.Statements.Count);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.UnexpectedToken, diagnostic.Code);
        Assert.Contains("';' after a variable declaration", diagnostic.Message);
        Assert.Contains("'let'", diagnostic.Message);
    }

    [Fact]
    public void Parser_SynchronizesMalformedStatementTailAtSemicolon()
    {
        var result = Parse("let x = 1 + * 2; let y = 3;");

        // The stray "2" belongs to the malformed first declaration. Recovery should
        // skip it rather than inventing a third top-level expression statement.
        Assert.Equal(2, result.Root.Statements.Count);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[0]);
        var second = Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Equal("y", second.Name);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.ExpectedExpression);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("';'"));
    }

    [Fact]
    public void Parser_StrayClosingBraceAtTopLevelDoesNotStall()
    {
        var result = Parse("} let x = 1;");

        Assert.True(result.Diagnostics.Count >= 1);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[^1]);
        Assert.Equal("x", Assert.IsType<VariableDeclaration>(result.Root.Statements[^1]).Name);
    }

    [Fact]
    public void Parser_EmptyExpressionStatementDoesNotHideFollowingDeclaration()
    {
        var result = Parse("; let x = 1;");

        Assert.Equal(2, result.Root.Statements.Count);
        Assert.IsType<ExpressionStatement>(result.Root.Statements[0]);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.ExpectedExpression);
    }

    [Fact]
    public void Parser_MissingIfConditionPreservesBody()
    {
        var result = Parse("if { let x = 1; } let y = 2;");

        Assert.Equal(2, result.Root.Statements.Count);
        var conditional = Assert.IsType<IfStatement>(result.Root.Statements[0]);
        Assert.IsType<VariableDeclaration>(Assert.Single(conditional.ThenBranch.Statements));
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.ExpectedExpression);
    }

    [Fact]
    public void Parser_MissingWhileConditionPreservesBody()
    {
        var result = Parse("while { break; } let y = 2;");

        Assert.Equal(2, result.Root.Statements.Count);
        var loop = Assert.IsType<WhileStatement>(result.Root.Statements[0]);
        Assert.IsType<BreakStatement>(Assert.Single(loop.Body.Statements));
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.ExpectedExpression);
    }

    [Fact]
    public void Parser_MissingForIterablePreservesBody()
    {
        var result = Parse("for item in { break; } let y = 2;");

        Assert.Equal(2, result.Root.Statements.Count);
        var loop = Assert.IsType<ForStatement>(result.Root.Statements[0]);
        Assert.Equal("item", loop.VariableName);
        Assert.IsType<BreakStatement>(Assert.Single(loop.Body.Statements));
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.ExpectedExpression);
    }

    [Fact]
    public void Parser_RecoversMissingCommaBetweenFunctionParameters()
    {
        var result = Parse("function add(a b) { return; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Equal(new[] { "a", "b" }, function.Parameters);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.UnexpectedToken, diagnostic.Code);
        Assert.Contains("between parameters", diagnostic.Message);
    }

    [Fact]
    public void Parser_MalformedParameterTokenRecoversToLaterParametersAndBody()
    {
        var result = Parse("function f(a, 123, b) { return; } let x = 1;");

        Assert.Equal(2, result.Root.Statements.Count);
        var function = Assert.IsType<FunctionDeclaration>(result.Root.Statements[0]);
        Assert.Equal(new[] { "a", "b" }, function.Parameters);
        Assert.IsType<ReturnStatement>(Assert.Single(function.Body.Statements));
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken
            && diagnostic.Message.Contains("parameter identifier"));
    }

    [Fact]
    public void Parser_MissingCallCloseParenthesisDoesNotHideNextDeclaration()
    {
        var result = Parse("print(1, 2; let y = 3;");

        Assert.Equal(2, result.Root.Statements.Count);
        Assert.IsType<ExpressionStatement>(result.Root.Statements[0]);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken
            && diagnostic.Message.Contains("')'"));
    }

    [Fact]
    public void Parser_MissingListElementRecoversAtComma()
    {
        var result = Parse("[1, , 3]; let x = 1;");

        Assert.Equal(2, result.Root.Statements.Count);
        Assert.IsType<ExpressionStatement>(result.Root.Statements[0]);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.ExpectedExpression);
    }

    [Fact]
    public void Parser_MultipleIndependentErrorsStillReachFinalStatement()
    {
        var source = """
            let a = ;
            if { let b = ; }
            function f(x y) { return 1 }
            let c = 3;
            """;

        var result = Parse(source);

        Assert.Equal(4, result.Root.Statements.Count);
        var finalDeclaration = Assert.IsType<VariableDeclaration>(result.Root.Statements[^1]);
        Assert.Equal("c", finalDeclaration.Name);
        Assert.True(result.Diagnostics.Count >= 4);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.ExpectedExpression);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("between parameters"));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("';' after a return statement"));
    }

    [Fact]
    public void Parser_MissingSemicolonDiagnosticPointsAtFollowingStatement()
    {
        var result = Parse("let x = 1\r\nlet y = 2;");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.UnexpectedToken, diagnostic.Code);
        Assert.Equal(new SourcePosition(11, 2, 1), diagnostic.Span.Start);
        Assert.Equal(new SourcePosition(14, 2, 4), diagnostic.Span.End);
        Assert.Equal(2, result.Root.Statements.Count);
    }

    private static ParseResult<CompilationUnit> Parse(string source) =>
        new Parser(new SourceText(source)).ParseCompilationUnit();
}
