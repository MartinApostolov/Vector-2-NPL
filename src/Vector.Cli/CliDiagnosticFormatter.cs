using System.Text;
using Vector.Core.Diagnostics;

namespace Vector.Cli;

/// <summary>
/// Formats Vector diagnostics for command-line display.
/// </summary>
public static class CliDiagnosticFormatter
{
    public static string Format(Diagnostic diagnostic, string filePath, string source)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A diagnostic file path cannot be empty.", nameof(filePath));
        }

        ArgumentNullException.ThrowIfNull(source);

        var severity = diagnostic.Severity.ToString().ToLowerInvariant();
        var header = $"{filePath}:{diagnostic.Span.Start.Line}:{diagnostic.Span.Start.Column}: " +
                     $"{severity} {diagnostic.Code}: {diagnostic.Message}";

        if (!TryGetLine(source, diagnostic.Span.Start.Line, out var sourceLine))
        {
            return header;
        }

        var startIndex = diagnostic.Span.Start.Column - 1;
        if (startIndex < 0 || startIndex > sourceLine.Length)
        {
            return header;
        }

        var markerLength = GetMarkerLength(diagnostic, sourceLine.Length, startIndex);
        var markerIndent = BuildMarkerIndent(sourceLine, startIndex);

        var builder = new StringBuilder();
        builder.AppendLine(header);
        builder.Append("    ").AppendLine(sourceLine);
        builder.Append("    ").Append(markerIndent).Append(new string('^', markerLength));
        return builder.ToString();
    }

    private static int GetMarkerLength(Diagnostic diagnostic, int lineLength, int startIndex)
    {
        if (diagnostic.Span.End.Line == diagnostic.Span.Start.Line
            && diagnostic.Span.End.Column > diagnostic.Span.Start.Column)
        {
            var requested = diagnostic.Span.End.Column - diagnostic.Span.Start.Column;
            var available = Math.Max(1, lineLength - startIndex);
            return Math.Max(1, Math.Min(requested, available));
        }

        if (diagnostic.Span.End.Line > diagnostic.Span.Start.Line)
        {
            return Math.Max(1, lineLength - startIndex);
        }

        return 1;
    }

    private static string BuildMarkerIndent(string sourceLine, int length)
    {
        if (length == 0)
        {
            return string.Empty;
        }

        var characters = sourceLine[..length]
            .Select(character => character == '\t' ? '\t' : ' ')
            .ToArray();
        return new string(characters);
    }

    private static bool TryGetLine(string source, int targetLine, out string line)
    {
        if (targetLine < 1)
        {
            line = string.Empty;
            return false;
        }

        var currentLine = 1;
        var lineStart = 0;

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (character != '\r' && character != '\n')
            {
                continue;
            }

            if (currentLine == targetLine)
            {
                line = source[lineStart..index];
                return true;
            }

            if (character == '\r' && index + 1 < source.Length && source[index + 1] == '\n')
            {
                index++;
            }

            currentLine++;
            lineStart = index + 1;
        }

        if (currentLine == targetLine)
        {
            line = source[lineStart..];
            return true;
        }

        line = string.Empty;
        return false;
    }
}
