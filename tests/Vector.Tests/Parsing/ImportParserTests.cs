using System.Text;
using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Source;
using Vector.Core.Syntax.Expressions;
using Vector.Core.Syntax.Statements;
using Xunit;

namespace Vector.Tests.Parsing;

public sealed class ImportParserTests
{
    [Fact]
    public void Parser_ParsesSimpleImport()
    {
        var result = Parse("import math;");

        var import = Assert.IsType<ImportStatement>(Assert.Single(result.Root.Statements));
        Assert.Equal(new[] { "math" }, import.PathSegments);
        Assert.Equal("math", import.QualifiedPath);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesQualifiedImport()
    {
        var result = Parse("import lib.geometry;");

        var import = Assert.IsType<ImportStatement>(Assert.Single(result.Root.Statements));
        Assert.Equal(new[] { "lib", "geometry" }, import.PathSegments);
        Assert.Equal("lib.geometry", import.QualifiedPath);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_NormalizesUnicodeImportSegments()
    {
        var decomposed = "cafe\u0301";
        var result = Parse($"import lib.{decomposed};");

        var import = Assert.IsType<ImportStatement>(Assert.Single(result.Root.Statements));
        Assert.Equal(decomposed.Normalize(NormalizationForm.FormC), import.PathSegments[1]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_AllowsMultipleImportsBeforeOtherTopLevelCode()
    {
        var result = Parse("import lib.geometry; import math; let x = 1;");

        Assert.Collection(
            result.Root.Statements,
            statement => Assert.IsType<ImportStatement>(statement),
            statement => Assert.IsType<ImportStatement>(statement),
            statement => Assert.IsType<VariableDeclaration>(statement));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ReportsImportAfterTopLevelStatement()
    {
        var result = Parse("let x = 1; import lib.geometry;");

        Assert.IsType<ImportStatement>(result.Root.Statements[1]);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidImportPlacement, diagnostic.Code);
        Assert.Contains("before other top-level", diagnostic.Message);
    }

    [Fact]
    public void Parser_ReportsImportInsideBlock()
    {
        var result = Parse("{ import lib.geometry; }");

        var block = Assert.IsType<BlockStatement>(Assert.Single(result.Root.Statements));
        Assert.IsType<ImportStatement>(Assert.Single(block.Statements));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidImportPlacement, diagnostic.Code);
        Assert.Contains("module level", diagnostic.Message);
    }

    [Fact]
    public void Parser_ReportsImportInsideFunction()
    {
        var result = Parse("function f() { import lib.geometry; return; }");

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Root.Statements));
        Assert.IsType<ImportStatement>(function.Body.Statements[0]);
        Assert.Equal(DiagnosticCode.InvalidImportPlacement, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Parser_ReportsMissingImportPath()
    {
        var result = Parse("import ;");

        var import = Assert.IsType<ImportStatement>(Assert.Single(result.Root.Statements));
        Assert.Equal("<missing>", Assert.Single(import.PathSegments));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("import path identifier"));
    }

    [Fact]
    public void Parser_ReportsMissingSegmentAfterDot()
    {
        var result = Parse("import lib.;");

        var import = Assert.IsType<ImportStatement>(Assert.Single(result.Root.Statements));
        Assert.Equal(new[] { "lib" }, import.PathSegments);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("after '.'"));
    }

    [Fact]
    public void Parser_RequiresSemicolonAfterImport()
    {
        var result = Parse("import lib.geometry");

        Assert.IsType<ImportStatement>(Assert.Single(result.Root.Statements));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("';'"));
    }

    [Fact]
    public void Parser_ParsesQualifiedNameExpression()
    {
        var result = Parse("lib.geometry.distance;");

        var statement = Assert.IsType<ExpressionStatement>(Assert.Single(result.Root.Statements));
        var qualified = Assert.IsType<QualifiedNameExpression>(statement.Expression);
        Assert.Equal(new[] { "lib", "geometry", "distance" }, qualified.PathSegments);
        Assert.Equal("lib.geometry.distance", qualified.QualifiedName);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesCallThroughFullyQualifiedName()
    {
        var result = Parse("lib.geometry.distance(a, b);");

        var statement = Assert.IsType<ExpressionStatement>(Assert.Single(result.Root.Statements));
        var call = Assert.IsType<CallExpression>(statement.Expression);
        var qualified = Assert.IsType<QualifiedNameExpression>(call.Callee);
        Assert.Equal("lib.geometry.distance", qualified.QualifiedName);
        Assert.Equal(2, call.Arguments.Count);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesIndexingAfterQualifiedName()
    {
        var result = Parse("lib.data.values[0];");

        var statement = Assert.IsType<ExpressionStatement>(Assert.Single(result.Root.Statements));
        var index = Assert.IsType<IndexExpression>(statement.Expression);
        Assert.Equal("lib.data.values", Assert.IsType<QualifiedNameExpression>(index.Target).QualifiedName);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_NormalizesUnicodeQualifiedNameSegments()
    {
        var decomposed = "cafe\u0301";
        var result = Parse($"lib.{decomposed}.value;");

        var statement = Assert.IsType<ExpressionStatement>(Assert.Single(result.Root.Statements));
        var qualified = Assert.IsType<QualifiedNameExpression>(statement.Expression);
        Assert.Equal(decomposed.Normalize(NormalizationForm.FormC), qualified.PathSegments[1]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ReportsMissingIdentifierInQualifiedName()
    {
        var result = Parse("lib.geometry.;");

        var statement = Assert.IsType<ExpressionStatement>(Assert.Single(result.Root.Statements));
        Assert.Equal("lib.geometry", Assert.IsType<QualifiedNameExpression>(statement.Expression).QualifiedName);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.UnexpectedToken && diagnostic.Message.Contains("after '.'"));
    }

    [Fact]
    public void Parser_DoesNotTreatOrdinaryNamesAsQualifiedNames()
    {
        var result = Parse("distance(a, b);");

        var statement = Assert.IsType<ExpressionStatement>(Assert.Single(result.Root.Statements));
        var call = Assert.IsType<CallExpression>(statement.Expression);
        Assert.IsType<NameExpression>(call.Callee);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_TracksImportAndQualifiedNameSpans()
    {
        var source = "import lib.geometry;\r\nlib.geometry.distance;";
        var result = Parse(source);

        var import = Assert.IsType<ImportStatement>(result.Root.Statements[0]);
        var statement = Assert.IsType<ExpressionStatement>(result.Root.Statements[1]);
        var qualified = Assert.IsType<QualifiedNameExpression>(statement.Expression);

        Assert.Equal(new SourcePosition(0, 1, 1), import.Span.Start);
        Assert.Equal(new SourcePosition(20, 1, 21), import.Span.End);
        Assert.Equal(new SourcePosition(22, 2, 1), qualified.Span.Start);
        Assert.Equal(new SourcePosition(43, 2, 22), qualified.Span.End);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_SkipsCommentsAroundImportAndQualifiedAccess()
    {
        var result = Parse("import lib /* path */ . geometry; lib.geometry /* call */ .distance();");

        Assert.IsType<ImportStatement>(result.Root.Statements[0]);
        var statement = Assert.IsType<ExpressionStatement>(result.Root.Statements[1]);
        Assert.IsType<CallExpression>(statement.Expression);
        Assert.Empty(result.Diagnostics);
    }

    private static ParseResult<Vector.Core.Syntax.CompilationUnit> Parse(string source) =>
        new Parser(new SourceText(source)).ParseCompilationUnit();
}
