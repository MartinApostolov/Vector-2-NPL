using Vector.Core.Diagnostics;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Diagnostics;

public sealed class DiagnosticTests
{
    private static readonly SourceSpan TestSpan = new(
        new SourcePosition(2, 1, 3),
        new SourcePosition(5, 1, 6));

    [Fact]
    public void Diagnostic_StoresStructuredData()
    {
        var diagnostic = new Diagnostic(
            DiagnosticCode.Unspecified,
            "Something went wrong.",
            DiagnosticSeverity.Error,
            TestSpan);

        Assert.Equal(DiagnosticCode.Unspecified, diagnostic.Code);
        Assert.Equal("Something went wrong.", diagnostic.Message);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(TestSpan, diagnostic.Span);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Diagnostic_RejectsEmptyMessages(string message)
    {
        Assert.Throws<ArgumentException>(() => new Diagnostic(
            DiagnosticCode.Unspecified,
            message,
            DiagnosticSeverity.Error,
            TestSpan));
    }

    [Fact]
    public void DiagnosticBag_AddPreservesInsertionOrder()
    {
        var first = CreateDiagnostic("First", DiagnosticSeverity.Warning);
        var second = CreateDiagnostic("Second", DiagnosticSeverity.Error);
        var bag = new DiagnosticBag();

        bag.Add(first);
        bag.Add(second);

        Assert.Equal(2, bag.Count);
        Assert.Same(first, bag[0]);
        Assert.Same(second, bag[1]);
        Assert.Equal(new[] { first, second }, bag.ToArray());
    }

    [Fact]
    public void DiagnosticBag_ReportCreatesAddsAndReturnsDiagnostic()
    {
        var bag = new DiagnosticBag();

        var diagnostic = bag.Report(
            DiagnosticCode.Unspecified,
            "Reported error.",
            DiagnosticSeverity.Error,
            TestSpan);

        Assert.Single(bag);
        Assert.Same(diagnostic, bag[0]);
        Assert.Equal("Reported error.", diagnostic.Message);
        Assert.Equal(TestSpan, diagnostic.Span);
    }

    [Fact]
    public void DiagnosticBag_HasErrorsOnlyWhenErrorIsPresent()
    {
        var bag = new DiagnosticBag();

        Assert.False(bag.HasErrors);

        bag.Add(CreateDiagnostic("Information", DiagnosticSeverity.Info));
        bag.Add(CreateDiagnostic("Warning", DiagnosticSeverity.Warning));
        Assert.False(bag.HasErrors);

        bag.Add(CreateDiagnostic("Error", DiagnosticSeverity.Error));
        Assert.True(bag.HasErrors);
    }

    [Fact]
    public void DiagnosticBag_AddRangeAppendsDiagnostics()
    {
        var bag = new DiagnosticBag();
        var diagnostics = new[]
        {
            CreateDiagnostic("First", DiagnosticSeverity.Warning),
            CreateDiagnostic("Second", DiagnosticSeverity.Error)
        };

        bag.AddRange(diagnostics);

        Assert.Equal(diagnostics, bag.ToArray());
    }

    [Fact]
    public void DiagnosticBag_AddRejectsNullDiagnostic()
    {
        var bag = new DiagnosticBag();

        Assert.Throws<ArgumentNullException>(() => bag.Add(null!));
    }

    [Fact]
    public void DiagnosticBag_AddRangeRejectsNullCollection()
    {
        var bag = new DiagnosticBag();

        Assert.Throws<ArgumentNullException>(() => bag.AddRange(null!));
    }

    private static Diagnostic CreateDiagnostic(string message, DiagnosticSeverity severity) =>
        new(DiagnosticCode.Unspecified, message, severity, TestSpan);
}
