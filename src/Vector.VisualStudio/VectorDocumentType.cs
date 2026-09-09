namespace Vector.VisualStudio;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

/// <summary>
/// Vector document-type contribution for .vec source files.
/// </summary>
public sealed partial class VectorExtension
{
    internal const string VectorDocumentTypeName = "vector";
    internal const string VectorFileExtension = ".vec";

#pragma warning disable VSEXTPREVIEW_LSP
    [VisualStudioContribution]
    public static DocumentTypeConfiguration VectorDocumentType => new(VectorDocumentTypeName)
    {
        FileExtensions = new[] { VectorFileExtension },
        BaseDocumentType = Microsoft.VisualStudio.Extensibility.LanguageServer.LanguageServerProvider.LanguageServerBaseDocumentType,
    };
#pragma warning restore VSEXTPREVIEW_LSP
}
