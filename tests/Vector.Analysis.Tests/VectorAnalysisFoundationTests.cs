namespace Vector.Analysis.Tests;

using Vector.Analysis.Documents;
using Vector.Analysis.Text;
using Vector.Analysis.Workspace;
using Xunit;

public sealed class VectorAnalysisFoundationTests
{
    [Fact]
    public void Analyze_PreservesSnapshotIdentityAndReportsParserDiagnostics()
    {
        VectorDocumentSnapshot document = VectorDocumentSnapshot.CreateInMemory("let value = ;", "generated.vec", 7);

        VectorAnalysisResult result = new VectorAnalyzer().Analyze(document);

        Assert.Same(document, result.Document);
        Assert.Equal(7, result.Document.Version);
        Assert.True(result.HasErrors);
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Equal("generated.vec", diagnostic.SourceName);
            Assert.Equal(document.Text, diagnostic.SourceText);
        });
    }

    [Fact]
    public void InMemoryDocument_DoesNotRequirePhysicalFile()
    {
        VectorDocumentSnapshot document = VectorDocumentSnapshot.CreateInMemory("let value = 1;", "npl-preview.vec");

        VectorAnalysisResult result = new VectorAnalyzer().Analyze(document);

        Assert.Null(document.FilePath);
        Assert.Equal("vector-memory", document.Uri.Scheme);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void LineMap_UsesZeroBasedUtf16Positions()
    {
        const string text = "let icon = \"😀\";\nlet value = 1;";
        var map = new VectorLineMap(text);
        int afterEmoji = text.IndexOf("😀", StringComparison.Ordinal) + "😀".Length;

        TextPosition position = map.GetPosition(afterEmoji);

        Assert.Equal(0, position.Line);
        Assert.Equal(afterEmoji, position.Character);
        Assert.Equal(afterEmoji, map.GetOffset(position));
        Assert.Equal(2, "😀".Length);
    }

    [Theory]
    [InlineData("first\nsecond", 6)]
    [InlineData("first\r\nsecond", 7)]
    [InlineData("first\rsecond", 6)]
    public void LineMap_HandlesSupportedLineEndings(string text, int secondLineOffset)
    {
        var map = new VectorLineMap(text);

        Assert.Equal(new TextPosition(1, 0), map.GetPosition(secondLineOffset));
        Assert.Equal(secondLineOffset, map.GetOffset(new TextPosition(1, 0)));
    }

    [Fact]
    public void Workspace_RejectsStaleReplacement()
    {
        var workspace = new VectorWorkspace();
        VectorDocumentSnapshot current = VectorDocumentSnapshot.CreateInMemory("let value = 2;", "cache.vec", 2);
        VectorDocumentSnapshot stale = VectorDocumentSnapshot.CreateInMemory("let value = 1;", "cache.vec", 1);

        Assert.True(workspace.TryUpdateDocument(current, out VectorAnalysisResult currentResult));
        Assert.False(workspace.TryUpdateDocument(stale, out VectorAnalysisResult retainedResult));

        Assert.Same(currentResult, retainedResult);
        Assert.Equal(2, retainedResult.Document.Version);
        Assert.Equal("let value = 2;", retainedResult.Document.Text);
    }

    [Fact]
    public void Workspace_ReplacesAndRemovesDocuments()
    {
        var workspace = new VectorWorkspace();
        VectorDocumentSnapshot first = VectorDocumentSnapshot.CreateInMemory("let value = ;", "cache.vec", 1);
        VectorDocumentSnapshot second = VectorDocumentSnapshot.CreateInMemory("let value = 1;", "cache.vec", 2);

        Assert.True(workspace.TryUpdateDocument(first, out _));
        Assert.True(workspace.TryUpdateDocument(second, out VectorAnalysisResult updated));
        Assert.False(updated.HasErrors);
        Assert.True(workspace.RemoveDocument(second.Uri));
        Assert.False(workspace.TryGetDocument(second.Uri, out _));
    }
}
