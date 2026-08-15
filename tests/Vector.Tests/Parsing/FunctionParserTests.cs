using System.Text;
using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Source;
using Vector.Core.Syntax.Expressions;
using Vector.Core.Syntax.Statements;
using Xunit;

namespace Vector.Tests.Parsing;

public sealed class FunctionParserTests
{
    [Fact]
    public void Parser_ParsesNamedFunctionWithoutParameters()
    {
        var result = Parse("function greet() { 1; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Equal("greet", function.Name);
        Assert.Empty(function.Parameters);
        Assert.Single(function.Body.Statements);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesFunctionParametersInOrder()
    {
        var result = Parse("function add(a, b, c) { return a; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Equal(new[] { "a", "b", "c" }, function.Parameters);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_NormalizesUnicodeFunctionAndParameterNames()
    {
        var functionName = "cafe\u0301";
        var parameterName = "role\u0301";
        var result = Parse($"function {functionName}({parameterName}) {{ return {parameterName}; }}");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Equal(functionName.Normalize(NormalizationForm.FormC), function.Name);
        Assert.Equal(parameterName.Normalize(NormalizationForm.FormC), Assert.Single(function.Parameters));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesReturnWithValue()
    {
        var result = Parse("function square(x) { return x * x; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        var statement = Assert.IsType<ReturnStatement>(Assert.Single(function.Body.Statements));
        Assert.IsType<BinaryExpression>(statement.Expression);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesBareReturn()
    {
        var result = Parse("function stop() { return; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        var statement = Assert.IsType<ReturnStatement>(Assert.Single(function.Body.Statements));
        Assert.Null(statement.Expression);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_AllowsReturnInsideNestedBlockWithinFunction()
    {
        var result = Parse("function choose() { if true { return 1; } return 2; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        var conditional = Assert.IsType<IfStatement>(function.Body.Statements[0]);
        Assert.IsType<ReturnStatement>(Assert.Single(conditional.ThenBranch.Statements));
        Assert.IsType<ReturnStatement>(function.Body.Statements[1]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_AllowsNestedFunctionDeclarations()
    {
        var result = Parse("function outer() { function inner() { return 1; } return; }");

        var outer = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.IsType<FunctionDeclaration>(outer.Body.Statements[0]);
        Assert.IsType<ReturnStatement>(outer.Body.Statements[1]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ReportsReturnOutsideFunction()
    {
        var result = Parse("return 1;");

        Assert.IsType<ReturnStatement>(Assert.Single(result.Root.Statements));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidReturn, diagnostic.Code);
        Assert.Contains("inside a function", diagnostic.Message);
    }

    [Fact]
    public void Parser_ReportsBareReturnOutsideFunction()
    {
        var result = Parse("return;");

        var statement = Assert.IsType<ReturnStatement>(Assert.Single(result.Root.Statements));
        Assert.Null(statement.Expression);
        Assert.Equal(DiagnosticCode.InvalidReturn, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Parser_ReportsDuplicateParameterNames()
    {
        var result = Parse("function repeat(value, value) { return value; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Equal(new[] { "value", "value" }, function.Parameters);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.DuplicateParameter, diagnostic.Code);
        Assert.Contains("value", diagnostic.Message);
    }

    [Fact]
    public void Parser_DetectsDuplicateParametersAfterUnicodeNormalization()
    {
        var decomposed = "cafe\u0301";
        var composed = decomposed.Normalize(NormalizationForm.FormC);
        var result = Parse($"function f({decomposed}, {composed}) {{ return; }}");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Equal(function.Parameters[0], function.Parameters[1]);
        Assert.Equal(DiagnosticCode.DuplicateParameter, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Parser_ReportsMissingFunctionName()
    {
        var result = Parse("function () { return; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Equal("<missing>", function.Name);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("identifier"));
    }

    [Fact]
    public void Parser_ReportsMissingOpeningParenthesis()
    {
        var result = Parse("function f x) { return; }");

        Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("'('") );
    }

    [Fact]
    public void Parser_ReportsMissingClosingParenthesis()
    {
        var result = Parse("function f(a, b { return a; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Equal(new[] { "a", "b" }, function.Parameters);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("')'"));
    }

    [Fact]
    public void Parser_ReportsMissingParameterAfterComma()
    {
        var result = Parse("function f(a, ) { return; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Single(function.Parameters);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("parameter identifier"));
    }

    [Fact]
    public void Parser_ReportsMissingFunctionBodyWithoutConsumingFollowingStatement()
    {
        var result = Parse("function f() let x = 1;");

        Assert.Equal(2, result.Root.Statements.Count);
        var function = Assert.IsType<FunctionDeclaration>(result.Root.Statements[0]);
        Assert.Empty(function.Body.Statements);
        Assert.IsType<VariableDeclaration>(result.Root.Statements[1]);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("'{'") );
    }

    [Fact]
    public void Parser_RequiresSemicolonAfterReturnValue()
    {
        var result = Parse("function f() { return 1 }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.IsType<ReturnStatement>(Assert.Single(function.Body.Statements));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("';'"));
    }

    [Fact]
    public void Parser_FunctionBoundaryPreventsBreakingEnclosingLoop()
    {
        var result = Parse("while true { function f() { break; } break; }");

        var loop = Assert.IsType<WhileStatement>(Assert.Single(result.Root.Statements));
        var function = Assert.IsType<FunctionDeclaration>(loop.Body.Statements[0]);
        Assert.IsType<BreakStatement>(Assert.Single(function.Body.Statements));
        Assert.IsType<BreakStatement>(loop.Body.Statements[1]);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidLoopControl, diagnostic.Code);
    }

    [Fact]
    public void Parser_AllowsLoopControlInLoopInsideFunction()
    {
        var result = Parse("function f() { while true { break; } return; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.IsType<WhileStatement>(function.Body.Statements[0]);
        Assert.IsType<ReturnStatement>(function.Body.Statements[1]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_TracksFunctionAndReturnSpans()
    {
        var source = "function f() {\r\n    return 1;\r\n}";
        var result = Parse(source);

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        var statement = Assert.IsType<ReturnStatement>(Assert.Single(function.Body.Statements));
        Assert.Equal(new SourcePosition(0, 1, 1), function.Span.Start);
        Assert.Equal(source.Length, function.Span.End.Offset);
        Assert.Equal(new SourcePosition(20, 2, 5), statement.Span.Start);
        Assert.Equal(new SourcePosition(29, 2, 14), statement.Span.End);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_SkipsCommentsAroundFunctionSyntax()
    {
        var result = Parse("function /* name */ f(a, /* second */ b) { // body\n return a; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.Equal(new[] { "a", "b" }, function.Parameters);
        Assert.Empty(result.Diagnostics);
    }

    private static ParseResult<Vector.Core.Syntax.CompilationUnit> Parse(string source) =>
        new Parser(new SourceText(source)).ParseCompilationUnit();
}
