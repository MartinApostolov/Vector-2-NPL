using System.Globalization;
using System.Text;
using Vector.Core.Lexing;
using Vector.Core.Syntax.Statements;

namespace Vector.Core.Modules;

/// <summary>
/// Identifies one Vector module by its full qualified path, such as <c>lib.geometry</c>.
/// </summary>
public sealed class ModuleId : IEquatable<ModuleId>
{
    private readonly string[] _segments;

    public ModuleId(IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        _segments = segments.ToArray();

        if (_segments.Length == 0)
        {
            throw new ArgumentException("A module id must contain at least one path segment.", nameof(segments));
        }

        foreach (var segment in _segments)
        {
            if (!IsValidIdentifier(segment))
            {
                throw new ArgumentException(
                    $"Module path segment '{segment}' is not a valid Vector identifier.",
                    nameof(segments));
            }
        }

        QualifiedName = string.Join('.', _segments);
    }

    public IReadOnlyList<string> Segments => Array.AsReadOnly(_segments);

    public string QualifiedName { get; }

    public static ModuleId FromImport(ImportStatement import)
    {
        ArgumentNullException.ThrowIfNull(import);
        return new ModuleId(import.PathSegments);
    }

    public bool Equals(ModuleId? other) =>
        other is not null && string.Equals(QualifiedName, other.QualifiedName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ModuleId other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(QualifiedName);

    public override string ToString() => QualifiedName;

    private static bool IsValidIdentifier(string? segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return false;
        }

        var runes = segment.EnumerateRunes().ToArray();
        if (KeywordTable.GetKind(segment) != TokenKind.Identifier
            || runes.Length == 0
            || !IsIdentifierStart(runes[0]))
        {
            return false;
        }

        for (var i = 1; i < runes.Length; i++)
        {
            if (!IsIdentifierPart(runes[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifierStart(Rune rune) =>
        rune.Value == '_' || IsUnicodeLetter(Rune.GetUnicodeCategory(rune));

    private static bool IsIdentifierPart(Rune rune)
    {
        if (rune.Value == '_')
        {
            return true;
        }

        var category = Rune.GetUnicodeCategory(rune);
        return IsUnicodeLetter(category)
            || category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark
                or UnicodeCategory.DecimalDigitNumber;
    }

    private static bool IsUnicodeLetter(UnicodeCategory category) =>
        category is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter;
}
