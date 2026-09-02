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

    [VisualStudioContribution]
    public static DocumentTypeConfiguration VectorDocumentType => new(VectorDocumentTypeName)
    {
        FileExtensions = new[] { VectorFileExtension },
        BaseDocumentType = DocumentType.KnownValues.Text,
    };
}
