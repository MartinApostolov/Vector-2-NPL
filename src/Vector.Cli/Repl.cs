using Vector.Core.Diagnostics;
using Vector.Core.Lexing;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Vector.Core.StandardLibrary;
using Vector.Core.Syntax.Statements;

namespace Vector.Cli;

/// <summary>
/// Interactive Vector read-evaluate-print loop with persistent top-level state.
/// </summary>
public sealed class Repl
{
    private const string ReplSourceName = "<repl>";
    private const string PrimaryPrompt = "vector> ";
    private const string ContinuationPrompt = "...> ";

    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly Interpreter _interpreter;

    public Repl(
        TextReader? input = null,
        TextWriter? output = null,
        TextWriter? error = null,
        string? programRoot = null,
        NativeModuleRegistry? nativeModules = null)
    {
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
        _error = error ?? Console.Error;

        var root = string.IsNullOrWhiteSpace(programRoot)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(programRoot);
        var moduleLoader = new ModuleLoader(
            new ModuleResolver(root),
            nativeModules ?? StandardLibraryRegistry.CreateDefault());
        _interpreter = new Interpreter(
            host: new VectorInputHost(_output.WriteLine, _input.ReadLine),
            moduleLoader: moduleLoader);
    }

    public int Run()
    {
        _output.WriteLine("Vector REPL. Type :exit or :quit to leave.");

        while (true)
        {
            var source = ReadSubmission();
            if (source is null)
            {
                return 0;
            }

            if (IsExitCommand(source))
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            ExecuteSubmission(source);
        }
    }

    private string? ReadSubmission()
    {
        var lines = new List<string>();

        while (true)
        {
            _output.Write(lines.Count == 0 ? PrimaryPrompt : ContinuationPrompt);
            _output.Flush();

            var line = _input.ReadLine();
            if (line is null)
            {
                return lines.Count == 0 ? null : string.Join(System.Environment.NewLine, lines);
            }

            if (lines.Count == 0 && IsExitCommand(line))
            {
                return line;
            }

            lines.Add(line);
            var source = string.Join(System.Environment.NewLine, lines);
            if (!NeedsMoreInput(source))
            {
                return source;
            }
        }
    }

    private void ExecuteSubmission(string source)
    {
        var parseResult = new Parser(new SourceText(source)).ParseCompilationUnit();
        if (parseResult.HasErrors)
        {
            WriteDiagnostics(parseResult.Diagnostics, source);
            return;
        }

        try
        {
            var value = _interpreter.Execute(parseResult.Root, ReplSourceName, source);
            if (parseResult.Root.Statements.LastOrDefault() is ExpressionStatement
                && value is not NothingValue)
            {
                _output.WriteLine(VectorValueFormatter.Format(value));
            }
        }
        catch (RuntimeError runtimeError)
        {
            WriteDiagnostic(
                new Diagnostic(
                    runtimeError.Code,
                    runtimeError.Message,
                    DiagnosticSeverity.Error,
                    runtimeError.Span,
                    runtimeError.SourceName,
                    runtimeError.SourceText),
                source);
        }
        catch (ModuleLoadException moduleError)
        {
            WriteModuleError(moduleError, source);
        }
    }

    private void WriteModuleError(ModuleLoadException error, string submissionSource)
    {
        if (error.Kind == ModuleLoadErrorKind.InvalidSyntax && error.Diagnostics.Count > 0)
        {
            WriteDiagnostics(error.Diagnostics, submissionSource);
            return;
        }

        var code = error.Kind switch
        {
            ModuleLoadErrorKind.ModuleNotFound => DiagnosticCode.ModuleNotFound,
            ModuleLoadErrorKind.CircularImport => DiagnosticCode.CircularImport,
            ModuleLoadErrorKind.IoFailure => DiagnosticCode.ModuleIoFailure,
            ModuleLoadErrorKind.ModuleConflict => DiagnosticCode.ModuleConflict,
            ModuleLoadErrorKind.NativeInitializationFailure => DiagnosticCode.NativeRuntimeFailure,
            _ => DiagnosticCode.Unspecified
        };
        var position = new SourcePosition(0, 1, 1);
        WriteDiagnostic(
            new Diagnostic(
                code,
                error.Message,
                DiagnosticSeverity.Error,
                new SourceSpan(position, position)),
            submissionSource);
    }

    private void WriteDiagnostics(IEnumerable<Diagnostic> diagnostics, string source)
    {
        foreach (var diagnostic in diagnostics)
        {
            WriteDiagnostic(diagnostic, source);
        }
    }

    private void WriteDiagnostic(Diagnostic diagnostic, string source) =>
        _error.WriteLine(CliDiagnosticFormatter.Format(diagnostic, ReplSourceName, source));

    private static bool IsExitCommand(string source)
    {
        var trimmed = source.Trim();
        return string.Equals(trimmed, ":exit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, ":quit", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NeedsMoreInput(string source)
    {
        var lexer = new Lexer(new SourceText(source));
        var parentheses = 0;
        var braces = 0;
        var brackets = 0;

        while (true)
        {
            var token = lexer.Lex();
            switch (token.Kind)
            {
                case TokenKind.OpenParen:
                    parentheses++;
                    break;
                case TokenKind.CloseParen:
                    parentheses--;
                    break;
                case TokenKind.OpenBrace:
                    braces++;
                    break;
                case TokenKind.CloseBrace:
                    braces--;
                    break;
                case TokenKind.OpenBracket:
                    brackets++;
                    break;
                case TokenKind.CloseBracket:
                    brackets--;
                    break;
            }

            if (parentheses < 0 || braces < 0 || brackets < 0)
            {
                return false;
            }

            if (token.Kind == TokenKind.EndOfFile)
            {
                return parentheses > 0 || braces > 0 || brackets > 0;
            }
        }
    }

}
