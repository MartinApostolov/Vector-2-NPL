namespace Vector.Analysis.Documents;

using Vector.Analysis.Text;
using Vector.Core.Source;

public sealed class VectorDocumentSnapshot
{
    public VectorDocumentSnapshot(
        Uri uri,
        string displayName,
        string text,
        int version,
        string? filePath = null,
        string? programRoot = null)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("A Vector document URI must be absolute.", nameof(uri));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A Vector document display name is required.", nameof(displayName));
        }

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        this.Uri = uri;
        this.DisplayName = displayName;
        this.Text = text ?? throw new ArgumentNullException(nameof(text));
        this.Version = version;
        this.FilePath = filePath;
        this.ProgramRoot = programRoot;
        this.SourceText = new SourceText(text);
        this.LineMap = new VectorLineMap(text);
    }

    public Uri Uri { get; }

    public string DisplayName { get; }

    public string Text { get; }

    public int Version { get; }

    public string? FilePath { get; }

    public string? ProgramRoot { get; }

    public SourceText SourceText { get; }

    public VectorLineMap LineMap { get; }

    public static VectorDocumentSnapshot CreateInMemory(
        string text,
        string sourceIdentity,
        int version = 0,
        string? programRoot = null)
    {
        if (string.IsNullOrWhiteSpace(sourceIdentity))
        {
            throw new ArgumentException("A source identity is required.", nameof(sourceIdentity));
        }

        string escapedIdentity = Uri.EscapeDataString(sourceIdentity);
        return new VectorDocumentSnapshot(
            new Uri($"vector-memory:///{escapedIdentity}"),
            sourceIdentity,
            text,
            version,
            programRoot: programRoot);
    }

    public static VectorDocumentSnapshot FromFile(string filePath, string text, int version, string? programRoot = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A source file path is required.", nameof(filePath));
        }

        string fullPath = Path.GetFullPath(filePath);
        return new VectorDocumentSnapshot(
            new Uri(fullPath),
            fullPath,
            text,
            version,
            fullPath,
            programRoot ?? Path.GetDirectoryName(fullPath));
    }
}
