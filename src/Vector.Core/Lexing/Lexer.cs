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
        SkipTrivia();

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

        if (IsAsciiDigit(_source[_position]))
        {
            return LexNumber();
        }

        if (_source[_position] == '"')
        {
            return LexString();
        }

        if (_source[_position] == '.' && IsAsciiDigit(Peek(1)))
        {
            return LexLeadingDotNumber();
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

    private Token LexNumber()
    {
        var start = _position;
        ConsumeAsciiDigits();

        if (Peek(0) == '.')
        {
            _position++;

            if (!IsAsciiDigit(Peek(0)))
            {
                return CreateMalformedNumberToken(start, _position);
            }

            ConsumeAsciiDigits();
        }

        if (Peek(0) is 'e' or 'E')
        {
            _position++;

            if (Peek(0) is '+' or '-')
            {
                _position++;
            }

            if (!IsAsciiDigit(Peek(0)))
            {
                return CreateMalformedNumberToken(start, _position);
            }

            ConsumeAsciiDigits();
        }

        var text = _source.Text[start.._position];
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value))
        {
            return CreateMalformedNumberToken(start, _position);
        }

        return CreateToken(TokenKind.Number, start, _position, value);
    }

    private Token LexLeadingDotNumber()
    {
        var start = _position;
        _position++;
        ConsumeAsciiDigits();

        if (Peek(0) is 'e' or 'E')
        {
            _position++;

            if (Peek(0) is '+' or '-')
            {
                _position++;
            }

            ConsumeAsciiDigits();
        }

        return CreateMalformedNumberToken(start, _position);
    }

    private Token CreateMalformedNumberToken(int start, int end)
    {
        var span = _source.GetSpan(start, end);
        var text = _source.Text[start..end];

        Diagnostics.Report(
            DiagnosticCode.MalformedNumber,
            $"Malformed number literal '{text}'.",
            DiagnosticSeverity.Error,
            span);

        return new Token(TokenKind.BadToken, text, null, span);
    }

    private Token LexString()
    {
        var start = _position;
        _position++;
        var value = new StringBuilder();

        while (_position < _source.Length)
        {
            var current = _source[_position];

            if (current == '"')
            {
                _position++;
                return CreateToken(TokenKind.String, start, _position, value.ToString());
            }

            if (current is '\r' or '\n')
            {
                return CreateUnterminatedStringToken(start);
            }

            if (current == '\\')
            {
                var escapeStart = _position;
                _position++;

                if (_position >= _source.Length || _source[_position] is '\r' or '\n')
                {
                    return CreateUnterminatedStringToken(start);
                }

                switch (_source[_position])
                {
                    case '"':
                        value.Append('"');
                        _position++;
                        break;
                    case '\\':
                        value.Append('\\');
                        _position++;
                        break;
                    case 'n':
                        value.Append('\n');
                        _position++;
                        break;
                    case 'r':
                        value.Append('\r');
                        _position++;
                        break;
                    case 't':
                        value.Append('\t');
                        _position++;
                        break;
                    default:
                        var escapedLength = GetScalarLength(_position);
                        var escapedText = _source.Text.Substring(_position, escapedLength);
                        _position += escapedLength;
                        value.Append(escapedText);

                        Diagnostics.Report(
                            DiagnosticCode.InvalidEscapeSequence,
                            $"Unknown escape sequence '\\{escapedText}'.",
                            DiagnosticSeverity.Error,
                            _source.GetSpan(escapeStart, _position));
                        break;
                }

                continue;
            }

            var scalarLength = GetScalarLength(_position);
            value.Append(_source.Text, _position, scalarLength);
            _position += scalarLength;
        }

        return CreateUnterminatedStringToken(start);
    }

    private Token CreateUnterminatedStringToken(int start)
    {
        var span = _source.GetSpan(start, _position);
        var text = _source.Text[start.._position];

        Diagnostics.Report(
            DiagnosticCode.UnterminatedString,
            "Unterminated string literal.",
            DiagnosticSeverity.Error,
            span);

        return new Token(TokenKind.BadToken, text, null, span);
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

    private void ConsumeAsciiDigits()
    {
        while (IsAsciiDigit(Peek(0)))
        {
            _position++;
        }
    }

    private void SkipTrivia()
    {
        while (true)
        {
            while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
            {
                _position++;
            }

            if (Peek(0) == '/' && Peek(1) == '/')
            {
                SkipLineComment();
                continue;
            }

            if (Peek(0) == '/' && Peek(1) == '*')
            {
                SkipBlockComment();
                continue;
            }

            return;
        }
    }

    private void SkipLineComment()
    {
        _position += 2;

        while (_position < _source.Length && _source[_position] is not '\r' and not '\n')
        {
            _position += GetScalarLength(_position);
        }
    }

    private void SkipBlockComment()
    {
        var start = _position;
        _position += 2;

        while (_position < _source.Length)
        {
            if (Peek(0) == '*' && Peek(1) == '/')
            {
                _position += 2;
                return;
            }

            _position += GetScalarLength(_position);
        }

        Diagnostics.Report(
            DiagnosticCode.UnterminatedBlockComment,
            "Unterminated block comment.",
            DiagnosticSeverity.Error,
            _source.GetSpan(start, _position));
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

    private static bool IsAsciiDigit(char? value)
    {
        return value is >= '0' and <= '9';
    }

    private int GetScalarLength(int index)
    {
        return index + 1 < _source.Length && char.IsSurrogatePair(_source.Text, index) ? 2 : 1;
    }
}
