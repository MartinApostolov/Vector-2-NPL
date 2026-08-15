using System.Globalization;
using System.Text;
using Vector.Core.Diagnostics;
using Vector.Core.Source;

namespace Vector.Core.Lexing;

/// <summary>
/// Converts Vector source text into lexical tokens.
/// </summary>
public sealed class Lexer
{
    private readonly SourceText _source;
    private int _position;

    public Lexer(SourceText source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public DiagnosticBag Diagnostics { get; } = new();

    public Token Lex()
    {
        SkipWhitespace();

        if (_position >= _source.Length)
        {
            var eofPosition = _source.GetPosition(_position);
            return new Token(
                TokenKind.EndOfFile,
                string.Empty,
                null,
                new SourceSpan(eofPosition, eofPosition));
        }

        if (IsIdentifierStart(_position))
        {
            return LexIdentifierOrKeyword();
        }

        var start = _position;
        var kind = LexOperatorOrPunctuation();
        if (kind != TokenKind.BadToken)
        {
            return CreateToken(kind, start, _position);
        }

        var invalidLength = GetScalarLength(_position);
        _position += invalidLength;
        var span = _source.GetSpan(start, _position);
        var text = _source.Text.Substring(start, invalidLength);

        Diagnostics.Report(
            DiagnosticCode.InvalidCharacter,
            $"Invalid character '{text}'.",
            DiagnosticSeverity.Error,
            span);

        return new Token(TokenKind.BadToken, text, null, span);
    }

    private Token LexIdentifierOrKeyword()
    {
        var start = _position;
        _position += GetScalarLength(_position);

        while (_position < _source.Length && IsIdentifierPart(_position))
        {
            _position += GetScalarLength(_position);
        }

        var text = _source.Text[start.._position];
        var normalized = text.Normalize(NormalizationForm.FormC);
        var kind = KeywordTable.GetKind(normalized);
        var value = kind == TokenKind.Identifier ? normalized : null;

        return CreateToken(kind, start, _position, value);
    }

    private TokenKind LexOperatorOrPunctuation()
    {
        switch (_source[_position])
        {
            case '+':
                _position++;
                return TokenKind.Plus;
            case '-':
                _position++;
                return TokenKind.Minus;
            case '*':
                _position++;
                return TokenKind.Star;
            case '/':
                _position++;
                return TokenKind.Slash;
            case '%':
                _position++;
                return TokenKind.Percent;
            case '<':
                _position++;
                if (Match('='))
                {
                    return TokenKind.LessOrEqual;
                }

                return TokenKind.Less;
            case '>':
                _position++;
                if (Match('='))
                {
                    return TokenKind.GreaterOrEqual;
                }

                return TokenKind.Greater;
            case '=':
                _position++;
                if (Match('='))
                {
                    return TokenKind.EqualEqual;
                }

                return TokenKind.Equals;
            case '!':
                if (Peek(1) == '=')
                {
                    _position += 2;
                    return TokenKind.BangEqual;
                }

                return TokenKind.BadToken;
            case '(':
                _position++;
                return TokenKind.OpenParen;
            case ')':
                _position++;
                return TokenKind.CloseParen;
            case '{':
                _position++;
                return TokenKind.OpenBrace;
            case '}':
                _position++;
                return TokenKind.CloseBrace;
            case '[':
                _position++;
                return TokenKind.OpenBracket;
            case ']':
                _position++;
                return TokenKind.CloseBracket;
            case ',':
                _position++;
                return TokenKind.Comma;
            case ';':
                _position++;
                return TokenKind.Semicolon;
            case '.':
                _position++;
                return TokenKind.Dot;
            default:
                return TokenKind.BadToken;
        }
    }

    private Token CreateToken(TokenKind kind, int start, int end, object? value = null)
    {
        return new Token(
            kind,
            _source.Text[start..end],
            value,
            _source.GetSpan(start, end));
    }

    private bool Match(char expected)
    {
        if (Peek(0) != expected)
        {
            return false;
        }

        _position++;
        return true;
    }

    private char? Peek(int offset)
    {
        var index = _position + offset;
        return index < _source.Length ? _source[index] : null;
    }

    private void SkipWhitespace()
    {
        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
        {
            _position++;
        }
    }

    private bool IsIdentifierStart(int index)
    {
        if (_source[index] == '_')
        {
            return true;
        }

        return IsUnicodeLetter(GetUnicodeCategory(index));
    }

    private bool IsIdentifierPart(int index)
    {
        if (_source[index] == '_')
        {
            return true;
        }

        var category = GetUnicodeCategory(index);
        return IsUnicodeLetter(category)
            || category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark
                or UnicodeCategory.DecimalDigitNumber;
    }

    private UnicodeCategory GetUnicodeCategory(int index)
    {
        return CharUnicodeInfo.GetUnicodeCategory(_source.Text, index);
    }

    private static bool IsUnicodeLetter(UnicodeCategory category)
    {
        return category is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter;
    }

    private int GetScalarLength(int index)
    {
        return index + 1 < _source.Length && char.IsSurrogatePair(_source.Text, index) ? 2 : 1;
    }
}
