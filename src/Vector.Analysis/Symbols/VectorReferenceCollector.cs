namespace Vector.Analysis.Symbols;

using Vector.Analysis.Documents;
using Vector.Analysis.Scopes;
using Vector.Core.Lexing;
using Vector.Core.Syntax;
using Vector.Core.Syntax.Expressions;
using Vector.Core.Syntax.Statements;

internal static class VectorReferenceCollector
{
    public static void Populate(
        VectorDocumentSnapshot document,
        CompilationUnit syntax,
        VectorSemanticModel semanticModel)
    {
        Token[] tokens = Lex(document);
        var references = new List<VectorReference>();
        var qualifiedReferences = new List<VectorQualifiedReference>();
        VisitStatements(syntax.Statements, semanticModel, tokens, references, qualifiedReferences);
        semanticModel.SetReferences(references, qualifiedReferences);
    }

    private static void VisitStatements(
        IReadOnlyList<StatementSyntax> statements,
        VectorSemanticModel model,
        Token[] tokens,
        List<VectorReference> references,
        List<VectorQualifiedReference> qualifiedReferences)
    {
        foreach (StatementSyntax statement in statements)
        {
            switch (statement)
            {
                case VariableDeclaration variable:
                    VisitExpression(variable.Initializer, model, tokens, references, qualifiedReferences);
                    break;
                case FunctionDeclaration function:
                    VisitStatements(function.Body.Statements, model, tokens, references, qualifiedReferences);
                    break;
                case BlockStatement block:
                    VisitStatements(block.Statements, model, tokens, references, qualifiedReferences);
                    break;
                case IfStatement conditional:
                    VisitExpression(conditional.Condition, model, tokens, references, qualifiedReferences);
                    VisitStatements(conditional.ThenBranch.Statements, model, tokens, references, qualifiedReferences);
                    if (conditional.ElseBranch is not null)
                    {
                        VisitStatements([conditional.ElseBranch], model, tokens, references, qualifiedReferences);
                    }

                    break;
                case WhileStatement loop:
                    VisitExpression(loop.Condition, model, tokens, references, qualifiedReferences);
                    VisitStatements(loop.Body.Statements, model, tokens, references, qualifiedReferences);
                    break;
                case ForStatement loop:
                    VisitExpression(loop.Iterable, model, tokens, references, qualifiedReferences);
                    VisitStatements(loop.Body.Statements, model, tokens, references, qualifiedReferences);
                    break;
                case ReturnStatement returnStatement when returnStatement.Expression is not null:
                    VisitExpression(returnStatement.Expression, model, tokens, references, qualifiedReferences);
                    break;
                case ExpressionStatement expressionStatement:
                    VisitExpression(expressionStatement.Expression, model, tokens, references, qualifiedReferences);
                    break;
            }
        }
    }

    private static void VisitExpression(
        ExpressionSyntax expression,
        VectorSemanticModel model,
        Token[] tokens,
        List<VectorReference> references,
        List<VectorQualifiedReference> qualifiedReferences)
    {
        switch (expression)
        {
            case NameExpression name:
                references.Add(new VectorReference(
                    name.Name,
                    name.Span,
                    model.TryResolveSymbol(name.Name, name.Span.Start.Offset, out VectorSymbol? symbol) ? symbol : null));
                break;
            case QualifiedNameExpression qualified:
                qualifiedReferences.Add(new VectorQualifiedReference(
                    qualified.PathSegments,
                    FindSegmentSpans(qualified, tokens),
                    qualified.Span));
                break;
            case AssignmentExpression assignment:
                VisitExpression(assignment.Target, model, tokens, references, qualifiedReferences);
                VisitExpression(assignment.Value, model, tokens, references, qualifiedReferences);
                break;
            case BinaryExpression binary:
                VisitExpression(binary.Left, model, tokens, references, qualifiedReferences);
                VisitExpression(binary.Right, model, tokens, references, qualifiedReferences);
                break;
            case UnaryExpression unary:
                VisitExpression(unary.Operand, model, tokens, references, qualifiedReferences);
                break;
            case GroupingExpression grouping:
                VisitExpression(grouping.Expression, model, tokens, references, qualifiedReferences);
                break;
            case ListExpression list:
                foreach (ExpressionSyntax element in list.Elements)
                {
                    VisitExpression(element, model, tokens, references, qualifiedReferences);
                }

                break;
            case CallExpression call:
                VisitExpression(call.Callee, model, tokens, references, qualifiedReferences);
                foreach (ExpressionSyntax argument in call.Arguments)
                {
                    VisitExpression(argument, model, tokens, references, qualifiedReferences);
                }

                break;
            case IndexExpression index:
                VisitExpression(index.Target, model, tokens, references, qualifiedReferences);
                VisitExpression(index.Index, model, tokens, references, qualifiedReferences);
                break;
        }
    }

    private static IReadOnlyList<Vector.Core.Source.SourceSpan> FindSegmentSpans(
        QualifiedNameExpression expression,
        Token[] tokens) => tokens
        .Where(token => token.Kind == TokenKind.Identifier
            && token.Span.Start.Offset >= expression.Span.Start.Offset
            && token.Span.End.Offset <= expression.Span.End.Offset)
        .Take(expression.PathSegments.Count)
        .Select(token => token.Span)
        .ToArray();

    private static Token[] Lex(VectorDocumentSnapshot document)
    {
        var lexer = new Lexer(document.SourceText);
        var tokens = new List<Token>();
        while (true)
        {
            Token token = lexer.Lex();
            tokens.Add(token);
            if (token.Kind == TokenKind.EndOfFile)
            {
                return tokens.ToArray();
            }
        }
    }
}
