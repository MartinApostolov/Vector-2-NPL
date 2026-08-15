using Vector.Core.Source;
using Vector.Core.Syntax;
using Vector.Core.Syntax.Expressions;
using Vector.Core.Syntax.Statements;
using Xunit;

namespace Vector.Tests.Syntax;

public sealed class StatementSyntaxTests
{
    private static readonly SourceSpan TestSpan = new(
        new SourcePosition(0, 1, 1),
        new SourcePosition(10, 1, 11));

    private static readonly SourceSpan InnerSpan = new(
        new SourcePosition(2, 1, 3),
        new SourcePosition(5, 1, 6));

    [Fact]
    public void ExpressionStatement_StoresExpressionAndSpan()
    {
        var expression = Literal(1d);
        var statement = new ExpressionStatement(expression, TestSpan);

        Assert.Same(expression, statement.Expression);
        Assert.Equal(TestSpan, statement.Span);
        Assert.IsAssignableFrom<StatementSyntax>(statement);
    }

    [Fact]
    public void ExpressionStatement_RejectsNullExpression()
    {
        Assert.Throws<ArgumentNullException>(() => new ExpressionStatement(null!, TestSpan));
    }

    [Fact]
    public void VariableDeclaration_StoresNameInitializerAndSpan()
    {
        var initializer = Literal(10d);
        var declaration = new VariableDeclaration("value", initializer, TestSpan);

        Assert.Equal("value", declaration.Name);
        Assert.Same(initializer, declaration.Initializer);
        Assert.Equal(TestSpan, declaration.Span);
    }

    [Fact]
    public void VariableDeclaration_RejectsInvalidInputs()
    {
        Assert.Throws<ArgumentException>(() => new VariableDeclaration(string.Empty, Literal(1d), TestSpan));
        Assert.Throws<ArgumentNullException>(() => new VariableDeclaration("value", null!, TestSpan));
    }

    [Fact]
    public void BlockStatement_CopiesItsStatementCollection()
    {
        var source = new List<StatementSyntax> { new BreakStatement(InnerSpan) };
        var block = new BlockStatement(source, TestSpan);
        source.Add(new ContinueStatement(InnerSpan));

        Assert.Single(block.Statements);
        Assert.IsType<BreakStatement>(block.Statements[0]);
        Assert.Equal(TestSpan, block.Span);
    }

    [Fact]
    public void BlockStatement_RejectsNullCollectionsAndElements()
    {
        Assert.Throws<ArgumentNullException>(() => new BlockStatement(null!, TestSpan));
        Assert.Throws<ArgumentException>(() => new BlockStatement(new StatementSyntax[] { null! }, TestSpan));
    }

    [Fact]
    public void IfStatement_StoresThenAndElseIfBranches()
    {
        var condition = new NameExpression("ready", InnerSpan);
        var thenBlock = EmptyBlock();
        var elseIf = new IfStatement(new NameExpression("retry", InnerSpan), EmptyBlock(), null, TestSpan);
        var statement = new IfStatement(condition, thenBlock, elseIf, TestSpan);

        Assert.Same(condition, statement.Condition);
        Assert.Same(thenBlock, statement.ThenBranch);
        Assert.Same(elseIf, statement.ElseBranch);
    }

    [Fact]
    public void IfStatement_AllowsNoElseBranchAndRejectsRequiredNulls()
    {
        var statement = new IfStatement(new NameExpression("ready", InnerSpan), EmptyBlock(), null, TestSpan);
        Assert.Null(statement.ElseBranch);

        Assert.Throws<ArgumentNullException>(() => new IfStatement(null!, EmptyBlock(), null, TestSpan));
        Assert.Throws<ArgumentNullException>(() => new IfStatement(Literal(true), null!, null, TestSpan));
    }

    [Fact]
    public void WhileStatement_StoresConditionAndBody()
    {
        var condition = new NameExpression("running", InnerSpan);
        var body = EmptyBlock();
        var statement = new WhileStatement(condition, body, TestSpan);

        Assert.Same(condition, statement.Condition);
        Assert.Same(body, statement.Body);
    }

    [Fact]
    public void ForStatement_StoresIterationPartsAndRejectsInvalidInputs()
    {
        var iterable = new NameExpression("items", InnerSpan);
        var body = EmptyBlock();
        var statement = new ForStatement("item", iterable, body, TestSpan);

        Assert.Equal("item", statement.VariableName);
        Assert.Same(iterable, statement.Iterable);
        Assert.Same(body, statement.Body);

        Assert.Throws<ArgumentException>(() => new ForStatement(string.Empty, iterable, body, TestSpan));
        Assert.Throws<ArgumentNullException>(() => new ForStatement("item", null!, body, TestSpan));
        Assert.Throws<ArgumentNullException>(() => new ForStatement("item", iterable, null!, TestSpan));
    }

    [Fact]
    public void FunctionDeclaration_CopiesParametersAndStoresBody()
    {
        var parameters = new List<string> { "a", "b" };
        var body = EmptyBlock();
        var declaration = new FunctionDeclaration("add", parameters, body, TestSpan);
        parameters.Add("c");

        Assert.Equal("add", declaration.Name);
        Assert.Equal(new[] { "a", "b" }, declaration.Parameters);
        Assert.Same(body, declaration.Body);
    }

    [Fact]
    public void FunctionDeclaration_RejectsInvalidRequiredInputsButAllowsDuplicateParametersInSyntax()
    {
        Assert.Throws<ArgumentException>(() => new FunctionDeclaration(string.Empty, Array.Empty<string>(), EmptyBlock(), TestSpan));
        Assert.Throws<ArgumentNullException>(() => new FunctionDeclaration("f", null!, EmptyBlock(), TestSpan));
        Assert.Throws<ArgumentException>(() => new FunctionDeclaration("f", new[] { "x", "" }, EmptyBlock(), TestSpan));
        Assert.Throws<ArgumentNullException>(() => new FunctionDeclaration("f", Array.Empty<string>(), null!, TestSpan));

        var duplicate = new FunctionDeclaration("f", new[] { "x", "x" }, EmptyBlock(), TestSpan);
        Assert.Equal(2, duplicate.Parameters.Count);
    }

    [Fact]
    public void ReturnStatement_RepresentsValueAndBareReturn()
    {
        var value = Literal(5d);
        var withValue = new ReturnStatement(value, TestSpan);
        var bare = new ReturnStatement(null, InnerSpan);

        Assert.Same(value, withValue.Expression);
        Assert.Null(bare.Expression);
    }

    [Fact]
    public void BreakAndContinueStatements_StoreTheirSpans()
    {
        Assert.Equal(TestSpan, new BreakStatement(TestSpan).Span);
        Assert.Equal(InnerSpan, new ContinueStatement(InnerSpan).Span);
    }

    [Fact]
    public void ImportStatement_StoresQualifiedPathSegmentsAndCopiesInput()
    {
        var segments = new List<string> { "lib", "geometry" };
        var import = new ImportStatement(segments, TestSpan);
        segments.Add("extra");

        Assert.Equal(new[] { "lib", "geometry" }, import.PathSegments);
        Assert.Equal("lib.geometry", import.QualifiedPath);
        Assert.Equal(TestSpan, import.Span);
    }

    [Fact]
    public void ImportStatement_RejectsEmptyOrInvalidPaths()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportStatement(null!, TestSpan));
        Assert.Throws<ArgumentException>(() => new ImportStatement(Array.Empty<string>(), TestSpan));
        Assert.Throws<ArgumentException>(() => new ImportStatement(new[] { "lib", "" }, TestSpan));
    }

    [Fact]
    public void CompilationUnit_CopiesStatementsAndActsAsSourceFileRoot()
    {
        var source = new List<StatementSyntax>
        {
            new ImportStatement(new[] { "lib", "geometry" }, InnerSpan),
            new ExpressionStatement(new NameExpression("run", InnerSpan), InnerSpan)
        };

        var unit = new CompilationUnit(source, TestSpan);
        source.Clear();

        Assert.Equal(2, unit.Statements.Count);
        Assert.IsType<ImportStatement>(unit.Statements[0]);
        Assert.IsType<ExpressionStatement>(unit.Statements[1]);
        Assert.Equal(TestSpan, unit.Span);
    }

    [Fact]
    public void CompilationUnit_RejectsNullCollectionsAndElements()
    {
        Assert.Throws<ArgumentNullException>(() => new CompilationUnit(null!, TestSpan));
        Assert.Throws<ArgumentException>(() => new CompilationUnit(new StatementSyntax[] { null! }, TestSpan));
    }

    private static LiteralExpression Literal(object? value) => new(value, InnerSpan);

    private static BlockStatement EmptyBlock() => new(Array.Empty<StatementSyntax>(), InnerSpan);
}
