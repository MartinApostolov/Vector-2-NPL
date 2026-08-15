namespace Vector.Core.Lexing;

/// <summary>
/// Identifies the lexical category of a Vector token.
/// </summary>
public enum TokenKind
{
    BadToken,
    EndOfFile,

    Identifier,
    Number,
    String,

    LetKeyword,
    IfKeyword,
    ElseKeyword,
    WhileKeyword,
    ForKeyword,
    InKeyword,
    FunctionKeyword,
    ReturnKeyword,
    BreakKeyword,
    ContinueKeyword,
    TrueKeyword,
    FalseKeyword,
    NothingKeyword,
    AndKeyword,
    OrKeyword,
    NotKeyword,
    ImportKeyword,

    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    EqualEqual,
    BangEqual,
    Equals,

    OpenParen,
    CloseParen,
    OpenBrace,
    CloseBrace,
    OpenBracket,
    CloseBracket,
    Comma,
    Semicolon,
    Dot
}
