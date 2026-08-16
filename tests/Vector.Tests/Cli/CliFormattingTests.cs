using Vector.Cli;
using Vector.Core.Diagnostics;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Cli;

public sealed class CliFormattingTests
{
    [Fact]
    public void FormatIncludesFileLineColumnSeverityCodeAndMessage()
    {
        const string source = "let value = 1;";
        var diagnostic = Error(source, 4, 9, DiagnosticCode.UnexpectedToken, "Unexpected token.");

        var formatted = CliDiagnosticFormatter.Format(diagnostic, "program.vec", source);

        Assert.StartsWith(
            "program.vec:1:5: error UnexpectedToken: Unexpected token.",
            formatted);
    }

    [Fact]
    public void FormatIncludesSourceLineAndMarker()
    {
        const string source = "let value = 1;";
        var diagnostic = Error(source, 4, 9);

        var formatted = CliDiagnosticFormatter.Format(diagnostic, "program.vec", source);

        Assert.Contains("    let value = 1;", formatted);
        Assert.Contains("        ^^^^^", formatted);
    }

    [Fact]
    public void FormatUsesSingleCaretForZeroLengthSpan()
    {
        const string source = "abc";
        var text = new SourceText(source);
        var diagnostic = new Diagnostic(
            DiagnosticCode.ExpectedExpression,
            "Expected expression.",
            DiagnosticSeverity.Error,
            text.GetSpan(3, 3));

        var formatted = CliDiagnosticFormatter.Format(diagnostic, "program.vec", source);

        Assert.EndsWith("       ^", formatted);
    }

    [Fact]
    public void FormatUsesOnlyFirstLineForMultiLineSpan()
    {
        const string source = "first line\nsecond line";
        var text = new SourceText(source);
        var diagnostic = new Diagnostic(
            DiagnosticCode.UnexpectedToken,
            "Bad range.",
            DiagnosticSeverity.Error,
            text.GetSpan(6, 17));

        var formatted = CliDiagnosticFormatter.Format(diagnostic, "program.vec", source);

        Assert.Contains("program.vec:1:7:", formatted);
        Assert.Contains("    first line", formatted);
        Assert.EndsWith("          ^^^^", formatted);
        Assert.DoesNotContain("second line", formatted);
    }

    [Fact]
    public void FormatSelectsCorrectLineForLfSource()
    {
        const string source = "one\ntwo\nthree";
        var diagnostic = Error(source, 4, 7);

        var formatted = CliDiagnosticFormatter.Format(diagnostic, "program.vec", source);

        Assert.Contains("program.vec:2:1:", formatted);
        Assert.Contains("    two", formatted);
    }

    [Fact]
    public void FormatSelectsCorrectLineForCrLfSource()
    {
        const string source = "one\r\ntwo\r\nthree";
        var diagnostic = Error(source, 5, 8);

        var formatted = CliDiagnosticFormatter.Format(diagnostic, "program.vec", source);

        Assert.Contains("program.vec:2:1:", formatted);
        Assert.Contains("    two", formatted);
    }

    [Fact]
    public void FormatCanMarkTrailingEmptyLine()
    {
        const string source = "one\n";
        var text = new SourceText(source);
        var diagnostic = new Diagnostic(
            DiagnosticCode.ExpectedExpression,
            "Expected expression.",
            DiagnosticSeverity.Error,
            text.GetSpan(source.Length, source.Length));

        var formatted = CliDiagnosticFormatter.Format(diagnostic, "program.vec", source);

        Assert.Contains("program.vec:2:1:", formatted);
        Assert.EndsWith("    ^", formatted);
    }

    [Fact]
    public void FormatPreservesTabsInMarkerIndent()
    {
        const string source = "\tvalue";
        var diagnostic = Error(source, 1, 6);

        var formatted = CliDiagnosticFormatter.Format(diagnostic, "program.vec", source);

        Assert.Contains("    \tvalue", formatted);
        Assert.EndsWith("    \t^^^^^", formatted);
    }

    [Fact]
    public void FormatFallsBackToHeaderWhenLineIsUnavailable()
    {
        const string source = "one line";
        var diagnostic = new Diagnostic(
            DiagnosticCode.Unspecified,
            "No source location.",
            DiagnosticSeverity.Error,
            new SourceSpan(
                new SourcePosition(0, 5, 1),
                new SourcePosition(0, 5, 1)));

        var formatted = CliDiagnosticFormatter.Format(diagnostic, "program.vec", source);

        Assert.Equal("program.vec:5:1: error Unspecified: No source location.", formatted);
    }

    [Fact]
    public void FormatSupportsWarningSeverity()
    {
        const string source = "value";
        var text = new SourceText(source);
        var diagnostic = new Diagnostic(
            DiagnosticCode.Unspecified,
            "Warning text.",
            DiagnosticSeverity.Warning,
            text.GetSpan(0, 5));

        var formatted = CliDiagnosticFormatter.Format(diagnostic, "program.vec", source);

        Assert.StartsWith("program.vec:1:1: warning Unspecified: Warning text.", formatted);
    }

    private static Diagnostic Error(
        string source,
        int startOffset,
        int endOffset,
        DiagnosticCode code = DiagnosticCode.Unspecified,
        string message = "Test error.")
    {
        var text = new SourceText(source);
        return new Diagnostic(
            code,
            message,
            DiagnosticSeverity.Error,
            text.GetSpan(startOffset, endOffset));
    }
}
