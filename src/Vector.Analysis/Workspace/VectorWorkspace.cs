namespace Vector.Analysis.Workspace;

using Vector.Analysis.Documents;

public sealed class VectorWorkspace
{
    private readonly object gate = new();
    private readonly Dictionary<Uri, VectorAnalysisResult> documents = new();
    private readonly VectorAnalyzer analyzer;

    public VectorWorkspace(VectorAnalyzer? analyzer = null)
    {
        this.analyzer = analyzer ?? new VectorAnalyzer();
    }

    public IReadOnlyList<VectorAnalysisResult> Documents
    {
        get
        {
            lock (this.gate)
            {
                return this.documents.Values.ToArray();
            }
        }
    }

    public bool TryUpdateDocument(
        VectorDocumentSnapshot document,
        out VectorAnalysisResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        VectorAnalysisResult analysis = this.analyzer.Analyze(document, cancellationToken);

        lock (this.gate)
        {
            if (this.documents.TryGetValue(document.Uri, out VectorAnalysisResult? current)
                && current.Document.Version >= document.Version)
            {
                result = current;
                return false;
            }

            this.documents[document.Uri] = analysis;
            result = analysis;
            return true;
        }
    }

    public bool TryGetDocument(Uri uri, out VectorAnalysisResult? result)
    {
        ArgumentNullException.ThrowIfNull(uri);
        lock (this.gate)
        {
            return this.documents.TryGetValue(uri, out result);
        }
    }

    public bool RemoveDocument(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        lock (this.gate)
        {
            return this.documents.Remove(uri);
        }
    }
}
