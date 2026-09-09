namespace Vector.Analysis.IntelliSense;

using Vector.Analysis.Documents;
using Vector.Analysis.Text;

public sealed record VectorHoverInfo(string Markdown, TextRange Range);

public sealed class VectorHoverService
{
    public VectorHoverInfo? GetHover(VectorAnalysisResult analysis, int utf16Offset)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        string text = analysis.Document.Text;
        if (utf16Offset < 0 || utf16Offset > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(utf16Offset));
        }

        int start = utf16Offset;
        int end = utf16Offset;
        while (start > 0 && VectorCompletionService.IsIdentifierOrDot(text[start - 1]))
        {
            start--;
        }

        while (end < text.Length && VectorCompletionService.IsIdentifierOrDot(text[end]))
        {
            end++;
        }

        if (start == end)
        {
            return null;
        }

        string token = text[start..end];
        (string signature, string documentation)? description = Describe(token);
        if (description is null)
        {
            return null;
        }

        return new VectorHoverInfo(
            $"```vector\n{description.Value.signature}\n```\n\n{description.Value.documentation}",
            new TextRange(analysis.Document.LineMap.GetPosition(start), analysis.Document.LineMap.GetPosition(end)));
    }

    private static (string Signature, string Documentation)? Describe(string token)
    {
        VectorCatalogItem? keyword = VectorLanguageCatalog.Keywords.SingleOrDefault(item => item.Label == token);
        if (keyword is not null)
        {
            return (keyword.Label, keyword.Documentation);
        }

        VectorCallableDescriptor? builtin = VectorLanguageCatalog.Builtins.SingleOrDefault(item => item.Name == token);
        if (builtin is not null)
        {
            return (builtin.Signature, builtin.Documentation);
        }

        VectorModuleDescriptor? module = VectorLanguageCatalog.StandardModules
            .SingleOrDefault(item => item.QualifiedName == token);
        if (module is not null)
        {
            return (module.QualifiedName, module.Documentation);
        }

        int dot = token.LastIndexOf('.');
        if (dot < 0)
        {
            return null;
        }

        module = VectorLanguageCatalog.StandardModules.SingleOrDefault(item => item.QualifiedName == token[..dot]);
        if (module is null)
        {
            return null;
        }

        string memberName = token[(dot + 1)..];
        VectorCallableDescriptor? function = module.Functions.SingleOrDefault(item => item.Name == memberName);
        if (function is not null)
        {
            return ($"{module.QualifiedName}.{function.Signature}", function.Documentation);
        }

        VectorCatalogItem? constant = module.Constants.SingleOrDefault(item => item.Label == memberName);
        return constant is null ? null : ($"{module.QualifiedName}.{constant.Label}", constant.Documentation);
    }
}
