namespace Vector.Core.Lexing;

/// <summary>
/// Maps Vector's reserved lowercase keyword spellings to token kinds.
/// </summary>
public static class KeywordTable
{
    private static readonly IReadOnlyDictionary<string, TokenKind> Keywords =
        new Dictionary<string, TokenKind>(StringComparer.Ordinal)
        {
            ["let"] = TokenKind.LetKeyword,
            ["if"] = TokenKind.IfKeyword,
            ["else"] = TokenKind.ElseKeyword,
            ["while"] = TokenKind.WhileKeyword,
            ["for"] = TokenKind.ForKeyword,
            ["in"] = TokenKind.InKeyword,
            ["function"] = TokenKind.FunctionKeyword,
            ["return"] = TokenKind.ReturnKeyword,
            ["break"] = TokenKind.BreakKeyword,
            ["continue"] = TokenKind.ContinueKeyword,
            ["true"] = TokenKind.TrueKeyword,
            ["false"] = TokenKind.FalseKeyword,
            ["nothing"] = TokenKind.NothingKeyword,
            ["and"] = TokenKind.AndKeyword,
            ["or"] = TokenKind.OrKeyword,
            ["not"] = TokenKind.NotKeyword,
            ["import"] = TokenKind.ImportKeyword
        };

    public static TokenKind GetKind(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Keywords.TryGetValue(text, out var kind) ? kind : TokenKind.Identifier;
    }
}
