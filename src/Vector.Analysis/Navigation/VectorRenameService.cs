namespace Vector.Analysis.Navigation;

using System.Text;
using Vector.Analysis.Documents;
using Vector.Analysis.Modules;
using Vector.Analysis.Scopes;
using Vector.Analysis.Symbols;
using Vector.Analysis.Text;
using Vector.Core.Lexing;
using Vector.Core.Source;

public enum VectorRenameFailureCode
{
    None,
    NotRenameable,
    InvalidIdentifier,
    NameConflict,
}

public sealed record VectorRenameEdit(Uri Uri, TextRange Range, string NewText);

public sealed record VectorRenameResult(
    bool Success,
    IReadOnlyList<VectorRenameEdit> Edits,
    VectorRenameFailureCode FailureCode,
    string? FailureMessage)
{
    public static VectorRenameResult Failed(VectorRenameFailureCode code, string message) =>
        new(false, [], code, message);

    public static VectorRenameResult Succeeded(IReadOnlyList<VectorRenameEdit> edits) =>
        new(true, edits, VectorRenameFailureCode.None, null);
}

public sealed class VectorRenameService
{
    private readonly VectorReferenceService references;

    public VectorRenameService(VectorModuleIndex modules)
    {
        this.references = new VectorReferenceService(modules);
    }

    public VectorRenameResult Rename(
        VectorAnalysisResult analysis,
        int utf16Offset,
        string newName,
        IEnumerable<VectorAnalysisResult> workspaceDocuments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(newName);
        ArgumentNullException.ThrowIfNull(workspaceDocuments);
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedName = newName.Normalize(NormalizationForm.FormC);
        if (!IsIdentifier(normalizedName))
        {
            return VectorRenameResult.Failed(
                VectorRenameFailureCode.InvalidIdentifier,
                $"'{newName}' is not a valid Vector identifier.");
        }

        VectorResolvedSymbol? target = this.references.ResolveSymbol(analysis, utf16Offset, cancellationToken);
        if (target is null || target.Symbol.IsSynthetic || target.Symbol.IsDuplicate)
        {
            return VectorRenameResult.Failed(
                VectorRenameFailureCode.NotRenameable,
                "The selected name is not a statically resolved Vector source symbol.");
        }

        VectorScope? declaringScope = FindDeclaringScope(target.Analysis.SemanticModel.RootScope, target.Symbol);
        if (declaringScope is null)
        {
            return VectorRenameResult.Failed(
                VectorRenameFailureCode.NotRenameable,
                "The selected symbol has no renameable source declaration.");
        }

        if (!string.Equals(target.Symbol.Name, normalizedName, StringComparison.Ordinal)
            && HasBindingConflict(target, declaringScope, normalizedName))
        {
            return VectorRenameResult.Failed(
                VectorRenameFailureCode.NameConflict,
                $"A symbol named '{normalizedName}' already exists in the same scope.");
        }

        IReadOnlyList<VectorReferenceLocation> locations = this.references.GetReferences(
            analysis,
            utf16Offset,
            workspaceDocuments,
            includeDeclaration: true,
            cancellationToken);
        if (locations.Count == 0)
        {
            return VectorRenameResult.Failed(
                VectorRenameFailureCode.NotRenameable,
                "The selected symbol has no renameable source locations.");
        }

        VectorRenameEdit[] edits = locations
            .Select(location => new VectorRenameEdit(location.Uri, location.Range, normalizedName))
            .ToArray();
        return VectorRenameResult.Succeeded(edits);
    }

    private static bool HasBindingConflict(
        VectorResolvedSymbol target,
        VectorScope declaringScope,
        string newName)
    {
        bool declarationConflict = declaringScope.Declarations.Any(symbol =>
            !ReferenceEquals(symbol, target.Symbol)
            && !symbol.IsSynthetic
            && string.Equals(symbol.Name, newName, StringComparison.Ordinal));
        if (declarationConflict)
        {
            return true;
        }

        return target.Analysis.SemanticModel.References
            .Where(reference => ReferenceEquals(reference.Symbol, target.Symbol))
            .Any(reference => target.Analysis.SemanticModel.TryResolveSymbol(
                newName,
                reference.Span.Start.Offset,
                out VectorSymbol? conflicting)
                && conflicting is not null
                && !ReferenceEquals(conflicting, target.Symbol));
    }

    private static bool IsIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var lexer = new Lexer(new SourceText(value));
        Token token = lexer.Lex();
        Token end = lexer.Lex();
        return token.Kind == TokenKind.Identifier
            && token.Span.Start.Offset == 0
            && token.Span.End.Offset == value.Length
            && end.Kind == TokenKind.EndOfFile
            && lexer.Diagnostics.Count == 0;
    }

    private static VectorScope? FindDeclaringScope(VectorScope scope, VectorSymbol target)
    {
        if (scope.Declarations.Any(symbol => ReferenceEquals(symbol, target)))
        {
            return scope;
        }

        foreach (VectorScope child in scope.Children)
        {
            VectorScope? result = FindDeclaringScope(child, target);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }
}
