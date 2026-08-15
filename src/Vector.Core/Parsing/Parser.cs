using Vector.Core.Diagnostics;
using Vector.Core.Lexing;
using Vector.Core.Source;
using Vector.Core.Syntax;
using Vector.Core.Syntax.Expressions;
using Vector.Core.Syntax.Statements;

namespace Vector.Core.Parsing;

/// <summary>
/// Parses Vector tokens into syntax nodes.
/// </summary>
public sealed class Parser
{
    private readonly List<Token> _tokens = new();
    private int _position;

    public Parser(SourceText source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var lexer = new Lexer(source);
        while (true)
        {
            var token = lexer.Lex();

            // Bad tokens are represented by lexer diagnostics and are not useful to the parser.
            if (token.Kind != TokenKind.BadToken)
            {
                _tokens.Add(token);
            }

            if (token.Kind == TokenKind.EndOfFile)
            {
                break;
            }
        }

        Diagnostics.AddRange(lexer.Diagnostics);
    }

    public DiagnosticBag Diagnostics { get; } = new();

    public ParseResult<ExpressionSyntax> ParseExpression()
    {
        var expression = ParseAssignmentExpression();

        if (Current.Kind != TokenKind.EndOfFile)
        {
            ReportUnexpectedToken(Current, "end of expression");
        }

        return new ParseResult<ExpressionSyntax>(expression, Diagnostics);
    }

    public ParseResult<CompilationUnit> ParseCompilationUnit()
    {
        var statements = new List<StatementSyntax>();

        while (Current.Kind != TokenKind.EndOfFile)
        {
            statements.Add(ParseStatement());
        }

        var eof = Current;
        var start = statements.Count > 0 ? statements[0].Span.Start : eof.Span.Start;
        var unit = new CompilationUnit(
            statements,
            new SourceSpan(start, eof.Span.End));

        return new ParseResult<CompilationUnit>(unit, Diagnostics);
    }

    private StatementSyntax ParseStatement()
    {
        return Current.Kind switch
        {
            TokenKind.LetKeyword => ParseVariableDeclaration(),
            TokenKind.OpenBrace => ParseBlockStatement(),
            TokenKind.IfKeyword => ParseIfStatement(),
            _ => ParseExpressionStatement()
        };
    }

    private VariableDeclaration ParseVariableDeclaration()
    {
        var letToken = NextToken();

        string name;
        if (Current.Kind == TokenKind.Identifier)
        {
            var nameToken = NextToken();
            name = nameToken.Value as string ?? nameToken.Text;
        }
        else
        {
            ReportUnexpectedToken(Current, "an identifier");
            name = string.Empty;
        }

        if (Current.Kind == TokenKind.Equals)
        {
            NextToken();
        }
        else
        {
            ReportUnexpectedToken(Current, "'='");
        }

        var initializer = ParseAssignmentExpression();
        var end = ConsumeStatementSemicolon(initializer.Span.End);

        // Invalid declarations still need an AST node so later parser recovery can continue.
        if (name.Length == 0)
        {
            name = "<missing>";
        }

        return new VariableDeclaration(
            name,
            initializer,
            new SourceSpan(letToken.Span.Start, end));
    }

    private ExpressionStatement ParseExpressionStatement()
    {
        var expression = ParseAssignmentExpression();
        var end = ConsumeStatementSemicolon(expression.Span.End);

        return new ExpressionStatement(
            expression,
            new SourceSpan(expression.Span.Start, end));
    }

    private BlockStatement ParseBlockStatement()
    {
        var openBrace = NextToken();
        var statements = new List<StatementSyntax>();

        while (Current.Kind != TokenKind.CloseBrace && Current.Kind != TokenKind.EndOfFile)
        {
            statements.Add(ParseStatement());
        }

        var end = statements.Count > 0 ? statements[^1].Span.End : openBrace.Span.End;
        if (Current.Kind == TokenKind.CloseBrace)
        {
            end = NextToken().Span.End;
        }
        else
        {
            ReportUnexpectedToken(Current, "'}'");
        }

        return new BlockStatement(
            statements,
            new SourceSpan(openBrace.Span.Start, end));
    }

    private IfStatement ParseIfStatement()
    {
        var ifToken = NextToken();
        var condition = ParseAssignmentExpression();
        var thenBranch = ParseRequiredBlock();
        StatementSyntax? elseBranch = null;

        if (Current.Kind == TokenKind.ElseKeyword)
        {
            NextToken();

            if (Current.Kind == TokenKind.IfKeyword)
            {
                elseBranch = ParseIfStatement();
            }
            else if (Current.Kind == TokenKind.OpenBrace)
            {
                elseBranch = ParseBlockStatement();
            }
            else
            {
                ReportUnexpectedToken(Current, "'if' or '{'");
                elseBranch = CreateMissingBlock(Current.Span);
            }
        }

        var end = elseBranch?.Span.End ?? thenBranch.Span.End;
        return new IfStatement(
            condition,
            thenBranch,
            elseBranch,
            new SourceSpan(ifToken.Span.Start, end));
    }

    private BlockStatement ParseRequiredBlock()
    {
        if (Current.Kind == TokenKind.OpenBrace)
        {
            return ParseBlockStatement();
        }

        ReportUnexpectedToken(Current, "'{'");
        return CreateMissingBlock(Current.Span);
    }

    private static BlockStatement CreateMissingBlock(SourceSpan span)
    {
        var position = span.Start;
        return new BlockStatement(
            Array.Empty<StatementSyntax>(),
            new SourceSpan(position, position));
    }

    private SourcePosition ConsumeStatementSemicolon(SourcePosition fallbackEnd)
    {
        if (Current.Kind == TokenKind.Semicolon)
        {
            return NextToken().Span.End;
        }

        ReportUnexpectedToken(Current, "';'");
        return fallbackEnd;
    }

    private ExpressionSyntax ParseAssignmentExpression()
    {
        var target = ParseBinaryExpression();
        if (Current.Kind != TokenKind.Equals)
        {
            return target;
        }

        var equalsToken = NextToken();
        var value = ParseAssignmentExpression();

        if (target is not NameExpression && target is not IndexExpression)
        {
            Diagnostics.Report(
                DiagnosticCode.InvalidAssignmentTarget,
                "The left side of an assignment must be a name or index expression.",
                DiagnosticSeverity.Error,
                target.Span);
        }

        return new AssignmentExpression(
            target,
            equalsToken,
            value,
            new SourceSpan(target.Span.Start, value.Span.End));
    }

    private ExpressionSyntax ParseBinaryExpression(int parentPrecedence = 0)
    {
        ExpressionSyntax left;
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);

        if (unaryPrecedence != 0)
        {
            var operatorToken = NextToken();
            var operand = ParseBinaryExpression(unaryPrecedence);
            left = new UnaryExpression(
                operatorToken,
                operand,
                new SourceSpan(operatorToken.Span.Start, operand.Span.End));
        }
        else
        {
            left = ParsePostfixExpression();
        }

        while (true)
        {
            var precedence = GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence)
            {
                break;
            }

            var operatorToken = NextToken();
            var right = ParseBinaryExpression(precedence);
            left = new BinaryExpression(
                left,
                operatorToken,
                right,
                new SourceSpan(left.Span.Start, right.Span.End));
        }

        return left;
    }

    private ExpressionSyntax ParsePostfixExpression()
    {
        var expression = ParsePrimaryExpression();

        while (true)
        {
            if (Current.Kind == TokenKind.OpenParen)
            {
                expression = ParseCallExpression(expression);
                continue;
            }

            if (Current.Kind == TokenKind.OpenBracket)
            {
                expression = ParseIndexExpression(expression);
                continue;
            }

            return expression;
        }
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        switch (Current.Kind)
        {
            case TokenKind.Number:
            case TokenKind.String:
            {
                var token = NextToken();
                return new LiteralExpression(token.Value, token.Span);
            }

            case TokenKind.TrueKeyword:
            {
                var token = NextToken();
                return new LiteralExpression(true, token.Span);
            }

            case TokenKind.FalseKeyword:
            {
                var token = NextToken();
                return new LiteralExpression(false, token.Span);
            }

            case TokenKind.NothingKeyword:
            {
                var token = NextToken();
                return new LiteralExpression(null, token.Span);
            }

            case TokenKind.Identifier:
            {
                var token = NextToken();
                var name = token.Value as string ?? token.Text;
                return new NameExpression(name, token.Span);
            }

            case TokenKind.OpenParen:
                return ParseGroupingExpression();

            case TokenKind.OpenBracket:
                return ParseListExpression();

            default:
                return ParseMissingExpression();
        }
    }

    private ExpressionSyntax ParseGroupingExpression()
    {
        var openParen = NextToken();
        var expression = ParseAssignmentExpression();
        var end = expression.Span.End;

        if (Current.Kind == TokenKind.CloseParen)
        {
            end = NextToken().Span.End;
        }
        else
        {
            ReportUnexpectedToken(Current, "')'");
        }

        return new GroupingExpression(
            expression,
            new SourceSpan(openParen.Span.Start, end));
    }

    private ExpressionSyntax ParseListExpression()
    {
        var openBracket = NextToken();
        var elements = new List<ExpressionSyntax>();

        if (Current.Kind != TokenKind.CloseBracket)
        {
            while (true)
            {
                elements.Add(ParseAssignmentExpression());

                if (Current.Kind != TokenKind.Comma)
                {
                    break;
                }

                NextToken();
            }
        }

        var end = elements.Count > 0 ? elements[^1].Span.End : openBracket.Span.End;
        if (Current.Kind == TokenKind.CloseBracket)
        {
            end = NextToken().Span.End;
        }
        else
        {
            ReportUnexpectedToken(Current, "']'");
        }

        return new ListExpression(
            elements,
            new SourceSpan(openBracket.Span.Start, end));
    }

    private ExpressionSyntax ParseCallExpression(ExpressionSyntax callee)
    {
        var openParen = NextToken();
        var arguments = new List<ExpressionSyntax>();

        if (Current.Kind != TokenKind.CloseParen)
        {
            while (true)
            {
                arguments.Add(ParseAssignmentExpression());

                if (Current.Kind != TokenKind.Comma)
                {
                    break;
                }

                NextToken();
            }
        }

        var end = arguments.Count > 0 ? arguments[^1].Span.End : openParen.Span.End;
        if (Current.Kind == TokenKind.CloseParen)
        {
            end = NextToken().Span.End;
        }
        else
        {
            ReportUnexpectedToken(Current, "')'");
        }

        return new CallExpression(
            callee,
            arguments,
            new SourceSpan(callee.Span.Start, end));
    }

    private ExpressionSyntax ParseIndexExpression(ExpressionSyntax target)
    {
        var openBracket = NextToken();

        ExpressionSyntax index;
        if (Current.Kind == TokenKind.CloseBracket || Current.Kind == TokenKind.EndOfFile)
        {
            Diagnostics.Report(
                DiagnosticCode.ExpectedExpression,
                $"Expected an expression, but found {Describe(Current)}.",
                DiagnosticSeverity.Error,
                Current.Span);
            index = CreateMissingExpression(Current.Span);
        }
        else
        {
            index = ParseAssignmentExpression();
        }

        var end = index.Span.End.Offset >= openBracket.Span.End.Offset
            ? index.Span.End
            : openBracket.Span.End;

        if (Current.Kind == TokenKind.CloseBracket)
        {
            end = NextToken().Span.End;
        }
        else
        {
            ReportUnexpectedToken(Current, "']'");
        }

        return new IndexExpression(
            target,
            index,
            new SourceSpan(target.Span.Start, end));
    }

    private ExpressionSyntax ParseMissingExpression()
    {
        var token = Current;
        Diagnostics.Report(
            DiagnosticCode.ExpectedExpression,
            $"Expected an expression, but found {Describe(token)}.",
            DiagnosticSeverity.Error,
            token.Span);

        if (token.Kind != TokenKind.EndOfFile)
        {
            NextToken();
        }

        return CreateMissingExpression(token.Span);
    }

    private static ExpressionSyntax CreateMissingExpression(SourceSpan span) =>
        new LiteralExpression(null, new SourceSpan(span.Start, span.Start));

    private void ReportUnexpectedToken(Token token, string expected)
    {
        Diagnostics.Report(
            DiagnosticCode.UnexpectedToken,
            $"Expected {expected}, but found {Describe(token)}.",
            DiagnosticSeverity.Error,
            token.Span);
    }

    private static string Describe(Token token) =>
        token.Kind == TokenKind.EndOfFile ? "end of file" : $"'{token.Text}'";

    private Token Current => Peek(0);

    private Token Peek(int offset)
    {
        var index = _position + offset;
        return index < _tokens.Count ? _tokens[index] : _tokens[^1];
    }

    private Token NextToken()
    {
        var current = Current;
        if (_position < _tokens.Count - 1)
        {
            _position++;
        }

        return current;
    }

    private static int GetUnaryPrecedence(TokenKind kind) =>
        kind is TokenKind.Minus or TokenKind.NotKeyword ? 7 : 0;

    private static int GetBinaryPrecedence(TokenKind kind) =>
        kind switch
        {
            TokenKind.Star or TokenKind.Slash or TokenKind.Percent => 6,
            TokenKind.Plus or TokenKind.Minus => 5,
            TokenKind.Less or TokenKind.LessOrEqual or TokenKind.Greater or TokenKind.GreaterOrEqual => 4,
            TokenKind.EqualEqual or TokenKind.BangEqual => 3,
            TokenKind.AndKeyword => 2,
            TokenKind.OrKeyword => 1,
            _ => 0
        };
}
