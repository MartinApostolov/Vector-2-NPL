namespace Vector.Analysis.Tests;

using Vector.Analysis.Documents;
using Vector.Analysis.Modules;
using Vector.Analysis.SignatureHelp;
using Xunit;

public sealed class VectorSignatureHelpTests
{
    [Fact]
    public void BuiltinCall_ReportsSignatureParameterCountAndActiveArgument()
    {
        const string source = "range(1, ";

        VectorSignatureInfo info = Get(source);

        Assert.Equal("range(start, end)", info.Label);
        Assert.Equal(["start", "end"], info.Parameters);
        Assert.Equal(1, info.ActiveParameter);
    }

    [Fact]
    public void UserFunction_UsesDeclaredParameterNames()
    {
        const string source = "function blend(left, right, weight) { return left; }\nblend(1, 2, ";

        VectorSignatureInfo info = Get(source);

        Assert.Equal("blend(left, right, weight)", info.Label);
        Assert.Equal(3, info.Parameters.Count);
        Assert.Equal(2, info.ActiveParameter);
    }

    [Fact]
    public void StandardModuleFunction_UsesQualifiedSignature()
    {
        const string source = "import lib.matrix;\nlib.matrix.multiply(first, ";

        VectorSignatureInfo info = Get(source);

        Assert.Equal("lib.matrix.multiply(a, b)", info.Label);
        Assert.Equal(1, info.ActiveParameter);
    }

    [Fact]
    public void NestedIncompleteCall_SelectsInnermostCallable()
    {
        const string source = "range(1, concat(\"left\", ";

        VectorSignatureInfo info = Get(source);

        Assert.Equal("concat(left, right)", info.Label);
        Assert.Equal(1, info.ActiveParameter);
    }

    [Fact]
    public void CommasInsideNestedCallsAndLists_DoNotAdvanceOuterArgument()
    {
        const string source = "range(length([1, 2, 3]), ";

        VectorSignatureInfo info = Get(source);

        Assert.Equal("range(start, end)", info.Label);
        Assert.Equal(1, info.ActiveParameter);
    }

    [Fact]
    public void MalformedCall_RemainsUsefulAtAComma()
    {
        const string source = "range(, ";

        VectorSignatureInfo info = Get(source);

        Assert.Equal("range(start, end)", info.Label);
        Assert.Equal(1, info.ActiveParameter);
    }

    [Theory]
    [InlineData("unknown(")]
    [InlineData("let value = 1;\nvalue(")]
    [InlineData("let values = [print];\nvalues[0](")]
    [InlineData("lib.vector.dot(")]
    public void UnknownDynamicNonCallableOrUnimportedTarget_HasNoSignature(string source)
    {
        VectorAnalysisResult analysis = Analyze(source);

        Assert.Null(new VectorSignatureHelpService(new VectorModuleIndex())
            .GetSignatureHelp(analysis, source.Length));
    }

    [Fact]
    public void ShadowingABuiltinWithVariable_SuppressesBuiltinSignature()
    {
        const string source = "let range = 1;\nrange(";
        VectorAnalysisResult analysis = Analyze(source);

        Assert.Null(new VectorSignatureHelpService(new VectorModuleIndex())
            .GetSignatureHelp(analysis, source.Length));
    }

    private static VectorSignatureInfo Get(string source) => Assert.IsType<VectorSignatureInfo>(
        new VectorSignatureHelpService(new VectorModuleIndex()).GetSignatureHelp(
            Analyze(source),
            source.Length));

    private static VectorAnalysisResult Analyze(string source) =>
        new VectorAnalyzer().Analyze(VectorDocumentSnapshot.CreateInMemory(source, "signatures.vec"));
}
