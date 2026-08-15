using Vector.Core.Source;

namespace Vector.Core.Lexing;

/// <summary>
/// A single lexical token produced from Vector source text.
/// </summary>
public sealed record Token
{
    public Token(TokenKind kind, string text, object? value, SourceSpan span)
    {
        ArgumentNullException.ThrowIfNull(text);

        Kind = kind;
        Text = text;
        Value = value;
        Span = span;
    }

    public TokenKind Kind { get; }

    /// <summary>
    /// The exact source text that produced this token.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The decoded literal or normalized identifier value when one is applicable.
    /// </summary>
    public object? Value { get; }

    public SourceSpan Span { get; }
}
