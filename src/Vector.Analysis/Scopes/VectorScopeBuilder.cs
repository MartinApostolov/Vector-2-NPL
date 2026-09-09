namespace Vector.Analysis.Scopes;

using Vector.Analysis.Documents;
using Vector.Analysis.Symbols;
using Vector.Core.Lexing;
using Vector.Core.Source;
using Vector.Core.Syntax;
using Vector.Core.Syntax.Statements;

internal sealed class VectorScopeBuilder
{
    private readonly VectorDocumentSnapshot document;
    private readonly Token[] tokens;
    private readonly List<VectorSymbol> symbols = [];

    private VectorScopeBuilder(VectorDocumentSnapshot document)
    {
        this.document = document;
        this.tokens = Lex(document.SourceText);
    }

    public static VectorSemanticModel Build(VectorDocumentSnapshot document, CompilationUnit syntax)
    {
        var builder = new VectorScopeBuilder(document);
        SourceSpan rootSpan = document.SourceText.GetSpan(0, document.Text.Length);
        var root = new VectorScope(VectorScopeKind.Module, rootSpan, parent: null, includesEnd: true);
        builder.VisitStatements(syntax.Statements, root, containingSymbol: null);
        return new VectorSemanticModel(root, builder.symbols);
    }

    private void VisitStatements(
        IReadOnlyList<StatementSyntax> statements,
        VectorScope scope,
        VectorSymbol? containingSymbol)
    {
        foreach (StatementSyntax statement in statements)
        {
            this.VisitStatement(statement, scope, containingSymbol);
        }
    }

    private void VisitStatement(StatementSyntax statement, VectorScope scope, VectorSymbol? containingSymbol)
    {
        switch (statement)
        {
            case VariableDeclaration variable:
                this.Declare(
                    variable.Name,
                    VectorSymbolKind.Variable,
                    this.FindDeclarationName(variable.Span, TokenKind.LetKeyword, variable.Name),
                    variable.Span,
                    variable.Span.End.Offset,
                    scope,
                    containingSymbol);
                break;

            case FunctionDeclaration function:
                this.VisitFunction(function, scope, containingSymbol);
                break;

            case BlockStatement block:
                this.VisitBlock(block, scope, containingSymbol, VectorScopeKind.Block);
                break;

            case IfStatement conditional:
                this.VisitBlock(conditional.ThenBranch, scope, containingSymbol, VectorScopeKind.Block);
                if (conditional.ElseBranch is BlockStatement elseBlock)
                {
                    this.VisitBlock(elseBlock, scope, containingSymbol, VectorScopeKind.Block);
                }
                else if (conditional.ElseBranch is IfStatement elseIf)
                {
                    this.VisitStatement(elseIf, scope, containingSymbol);
                }

                break;

            case WhileStatement loop:
                this.VisitBlock(loop.Body, scope, containingSymbol, VectorScopeKind.Block);
                break;

            case ForStatement loop:
                this.VisitFor(loop, scope, containingSymbol);
                break;
        }
    }

    private void VisitFunction(FunctionDeclaration function, VectorScope parent, VectorSymbol? containingSymbol)
    {
        VectorSymbol symbol = this.Declare(
            function.Name,
            VectorSymbolKind.Function,
            this.FindDeclarationName(function.Span, TokenKind.FunctionKeyword, function.Name),
            function.Span,
            function.Span.Start.Offset,
            parent,
            containingSymbol,
            function.Parameters);

        var functionScope = new VectorScope(
            VectorScopeKind.Function,
            this.GetScopeSpan(function.Body),
            parent,
            this.IsUnclosedBlock(function.Body));
        foreach ((string name, SourceSpan span) in this.FindParameters(function))
        {
            this.Declare(
                name,
                VectorSymbolKind.Parameter,
                span,
                span,
                function.Body.Span.Start.Offset,
                functionScope,
                symbol);
        }

        this.VisitBlock(function.Body, functionScope, symbol, VectorScopeKind.Block);
    }

    private void VisitFor(ForStatement loop, VectorScope parent, VectorSymbol? containingSymbol)
    {
        var scope = new VectorScope(
            VectorScopeKind.ForLoop,
            this.GetScopeSpan(loop.Body),
            parent,
            this.IsUnclosedBlock(loop.Body));
        this.Declare(
            loop.VariableName,
            VectorSymbolKind.LoopVariable,
            this.FindDeclarationName(loop.Span, TokenKind.ForKeyword, loop.VariableName),
            loop.Body.Span,
            loop.Body.Span.Start.Offset,
            scope,
            containingSymbol);
        this.VisitStatements(loop.Body.Statements, scope, containingSymbol);
    }

    private void VisitBlock(
        BlockStatement block,
        VectorScope parent,
        VectorSymbol? containingSymbol,
        VectorScopeKind kind)
    {
        var scope = new VectorScope(kind, this.GetScopeSpan(block), parent, this.IsUnclosedBlock(block));
        this.VisitStatements(block.Statements, scope, containingSymbol);
    }

    private VectorSymbol Declare(
        string name,
        VectorSymbolKind kind,
        SourceSpan declarationSpan,
        SourceSpan fullSpan,
        int availableFrom,
        VectorScope scope,
        VectorSymbol? containingSymbol,
        IReadOnlyList<string>? parameters = null)
    {
        var symbol = new VectorSymbol(
            name,
            kind,
            declarationSpan,
            fullSpan,
            availableFrom,
            parameters,
            containingSymbol);
        if (!symbol.IsSynthetic)
        {
            scope.Declare(symbol);
        }

        this.symbols.Add(symbol);
        if (!symbol.IsSynthetic)
        {
            containingSymbol?.AddChild(symbol);
        }

        return symbol;
    }

    private SourceSpan FindDeclarationName(SourceSpan span, TokenKind keyword, string expectedName)
    {
        Token? declarationKeyword = this.tokens.FirstOrDefault(token =>
            token.Kind == keyword && token.Span.Start.Offset == span.Start.Offset);
        Token? name = this.tokens.FirstOrDefault(token =>
            token.Kind == TokenKind.Identifier
            && token.Span.Start.Offset >= (declarationKeyword?.Span.End.Offset ?? span.Start.Offset)
            && token.Span.End.Offset <= span.End.Offset
            && GetName(token) == expectedName);
        return name?.Span ?? new SourceSpan(span.Start, span.Start);
    }

    private IEnumerable<(string Name, SourceSpan Span)> FindParameters(FunctionDeclaration function)
    {
        Token[] header = this.tokens.Where(token =>
            token.Span.Start.Offset >= function.Span.Start.Offset
            && token.Span.End.Offset <= function.Body.Span.Start.Offset).ToArray();
        int openParen = Array.FindIndex(header, token => token.Kind == TokenKind.OpenParen);
        if (openParen < 0)
        {
            yield break;
        }

        int parameterIndex = 0;
        for (int index = openParen + 1; index < header.Length && parameterIndex < function.Parameters.Count; index++)
        {
            Token token = header[index];
            if (token.Kind is TokenKind.CloseParen or TokenKind.OpenBrace)
            {
                yield break;
            }

            if (token.Kind == TokenKind.Identifier && GetName(token) == function.Parameters[parameterIndex])
            {
                yield return (function.Parameters[parameterIndex], token.Span);
                parameterIndex++;
            }
        }
    }

    private bool IsUnclosedBlock(BlockStatement block) =>
        block.Span.End.Offset == 0
        || block.Span.End.Offset > this.document.Text.Length
        || this.document.Text[block.Span.End.Offset - 1] != '}';

    private SourceSpan GetScopeSpan(BlockStatement block) => this.IsUnclosedBlock(block)
        ? this.document.SourceText.GetSpan(block.Span.Start.Offset, this.document.Text.Length)
        : block.Span;

    private static Token[] Lex(SourceText source)
    {
        var lexer = new Lexer(source);
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

    private static string GetName(Token token) => token.Value as string ?? token.Text;
}
