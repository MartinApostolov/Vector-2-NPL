namespace Vector.Analysis.SignatureHelp;

using Vector.Analysis.Documents;
using Vector.Analysis.IntelliSense;
using Vector.Analysis.Modules;
using Vector.Analysis.Symbols;
using Vector.Core.Lexing;
using Vector.Core.Syntax;
using Vector.Core.Syntax.Expressions;
using Vector.Core.Syntax.Statements;

public sealed record VectorSignatureInfo(
    string Label,
    IReadOnlyList<string> Parameters,
    string Documentation,
    int ActiveParameter);

public sealed class VectorSignatureHelpService
{
    private readonly VectorModuleIndex modules;

    public VectorSignatureHelpService(VectorModuleIndex modules)
    {
        this.modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }

    public VectorSignatureInfo? GetSignatureHelp(
        VectorAnalysisResult analysis,
        int utf16Offset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (utf16Offset < 0 || utf16Offset > analysis.Document.Text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(utf16Offset));
        }

        cancellationToken.ThrowIfCancellationRequested();
        Token[] tokens = Lex(analysis);
        CallContext? context = CollectCalls(analysis.Syntax)
            .Select(call => CreateContext(call, tokens, analysis.Document.Text.Length))
            .Where(context => context is not null
                && utf16Offset >= context.OpenParen.Span.End.Offset
                && utf16Offset <= context.ContextEnd)
            .OrderByDescending(context => context!.OpenParen.Span.Start.Offset)
            .FirstOrDefault();
        if (context is null)
        {
            return null;
        }

        Callable? callable = this.ResolveCallable(analysis, context.Call.Callee, cancellationToken);
        if (callable is null)
        {
            return null;
        }

        int activeParameter = CountActiveParameter(tokens, context.OpenParen, utf16Offset);
        if (callable.Parameters.Count > 0)
        {
            activeParameter = Math.Min(activeParameter, callable.Parameters.Count - 1);
        }
        else
        {
            activeParameter = 0;
        }

        return new VectorSignatureInfo(
            $"{callable.Name}({string.Join(", ", callable.Parameters)})",
            callable.Parameters,
            callable.Documentation,
            activeParameter);
    }

    private Callable? ResolveCallable(
        VectorAnalysisResult analysis,
        ExpressionSyntax callee,
        CancellationToken cancellationToken)
    {
        if (callee is NameExpression name)
        {
            VectorReference? reference = analysis.SemanticModel.References.FirstOrDefault(candidate =>
                candidate.Span == name.Span);
            if (reference?.Symbol is VectorSymbol symbol)
            {
                return symbol.Kind == VectorSymbolKind.Function
                    ? new Callable(
                        symbol.Name,
                        symbol.Parameters,
                        "Function declared in the current Vector document.")
                    : null;
            }

            VectorCallableDescriptor? builtin = VectorLanguageCatalog.Builtins
                .SingleOrDefault(candidate => candidate.Name == name.Name);
            return builtin is null
                ? null
                : new Callable(builtin.Name, builtin.Parameters, builtin.Documentation);
        }

        if (callee is not QualifiedNameExpression qualified || qualified.PathSegments.Count < 2)
        {
            return null;
        }

        string moduleName = string.Join('.', qualified.PathSegments.Take(qualified.PathSegments.Count - 1));
        string memberName = qualified.PathSegments[^1];
        bool imported = analysis.Syntax.Statements.OfType<ImportStatement>()
            .Any(import => import.QualifiedPath == moduleName);
        if (!imported)
        {
            return null;
        }

        VectorModuleDescriptor? standardModule = VectorLanguageCatalog.StandardModules
            .SingleOrDefault(module => module.QualifiedName == moduleName);
        VectorCallableDescriptor? standardFunction = standardModule?.Functions
            .SingleOrDefault(function => function.Name == memberName);
        if (standardFunction is not null)
        {
            return new Callable(
                $"{moduleName}.{standardFunction.Name}",
                standardFunction.Parameters,
                standardFunction.Documentation);
        }

        if (standardModule is not null
            || !this.modules.TryGetImportedModule(
                analysis,
                moduleName,
                out VectorModuleAnalysis? localModule,
                cancellationToken))
        {
            return null;
        }

        VectorSymbol? localFunction = localModule!.Members.SingleOrDefault(symbol =>
            symbol.Name == memberName && symbol.Kind == VectorSymbolKind.Function);
        return localFunction is null
            ? null
            : new Callable(
                $"{moduleName}.{localFunction.Name}",
                localFunction.Parameters,
                "Function exported by a local Vector module.");
    }

    private static int CountActiveParameter(Token[] tokens, Token openParen, int offset)
    {
        int active = 0;
        int depth = 0;
        foreach (Token token in tokens.Where(token =>
            token.Span.Start.Offset >= openParen.Span.End.Offset
            && token.Span.Start.Offset < offset))
        {
            switch (token.Kind)
            {
                case TokenKind.OpenParen:
                case TokenKind.OpenBracket:
                case TokenKind.OpenBrace:
                    depth++;
                    break;
                case TokenKind.CloseParen:
                case TokenKind.CloseBracket:
                case TokenKind.CloseBrace:
                    if (depth == 0)
                    {
                        return active;
                    }

                    depth--;
                    break;
                case TokenKind.Comma when depth == 0:
                    active++;
                    break;
            }
        }

        return active;
    }

    private static CallContext? CreateContext(CallExpression call, Token[] tokens, int documentLength)
    {
        Token? openParen = tokens.FirstOrDefault(token => token.Kind == TokenKind.OpenParen
            && token.Span.Start.Offset >= call.Callee.Span.End.Offset
            && token.Span.Start.Offset < call.Span.End.Offset);
        if (openParen is null)
        {
            return null;
        }

        Token? closeParen = tokens.FirstOrDefault(token =>
            token.Kind == TokenKind.CloseParen && token.Span.End.Offset == call.Span.End.Offset);
        int contextEnd = closeParen?.Span.Start.Offset ?? documentLength;
        return new CallContext(call, openParen, contextEnd);
    }

    private static IEnumerable<CallExpression> CollectCalls(CompilationUnit syntax)
    {
        var calls = new List<CallExpression>();
        VisitStatements(syntax.Statements, calls);
        return calls;
    }

    private static void VisitStatements(IReadOnlyList<StatementSyntax> statements, List<CallExpression> calls)
    {
        foreach (StatementSyntax statement in statements)
        {
            switch (statement)
            {
                case VariableDeclaration variable:
                    VisitExpression(variable.Initializer, calls);
                    break;
                case FunctionDeclaration function:
                    VisitStatements(function.Body.Statements, calls);
                    break;
                case BlockStatement block:
                    VisitStatements(block.Statements, calls);
                    break;
                case IfStatement conditional:
                    VisitExpression(conditional.Condition, calls);
                    VisitStatements(conditional.ThenBranch.Statements, calls);
                    if (conditional.ElseBranch is not null)
                    {
                        VisitStatements([conditional.ElseBranch], calls);
                    }

                    break;
                case WhileStatement loop:
                    VisitExpression(loop.Condition, calls);
                    VisitStatements(loop.Body.Statements, calls);
                    break;
                case ForStatement loop:
                    VisitExpression(loop.Iterable, calls);
                    VisitStatements(loop.Body.Statements, calls);
                    break;
                case ReturnStatement returnStatement when returnStatement.Expression is not null:
                    VisitExpression(returnStatement.Expression, calls);
                    break;
                case ExpressionStatement expressionStatement:
                    VisitExpression(expressionStatement.Expression, calls);
                    break;
            }
        }
    }

    private static void VisitExpression(ExpressionSyntax expression, List<CallExpression> calls)
    {
        switch (expression)
        {
            case CallExpression call:
                VisitExpression(call.Callee, calls);
                foreach (ExpressionSyntax argument in call.Arguments)
                {
                    VisitExpression(argument, calls);
                }

                calls.Add(call);
                break;
            case AssignmentExpression assignment:
                VisitExpression(assignment.Target, calls);
                VisitExpression(assignment.Value, calls);
                break;
            case BinaryExpression binary:
                VisitExpression(binary.Left, calls);
                VisitExpression(binary.Right, calls);
                break;
            case UnaryExpression unary:
                VisitExpression(unary.Operand, calls);
                break;
            case GroupingExpression grouping:
                VisitExpression(grouping.Expression, calls);
                break;
            case ListExpression list:
                foreach (ExpressionSyntax element in list.Elements)
                {
                    VisitExpression(element, calls);
                }

                break;
            case IndexExpression index:
                VisitExpression(index.Target, calls);
                VisitExpression(index.Index, calls);
                break;
        }
    }

    private static Token[] Lex(VectorAnalysisResult analysis)
    {
        var lexer = new Lexer(analysis.Document.SourceText);
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

    private sealed record Callable(string Name, IReadOnlyList<string> Parameters, string Documentation);

    private sealed record CallContext(CallExpression Call, Token OpenParen, int ContextEnd);
}
