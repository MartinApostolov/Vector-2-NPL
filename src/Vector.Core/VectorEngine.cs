using Vector.Core.Diagnostics;
using Vector.Core.Execution;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Vector.Core.StandardLibrary;

namespace Vector.Core;

/// <summary>
/// High-level reusable entry point for executing Vector source text.
/// </summary>
public sealed class VectorEngine
{
    public VectorEngine(NativeModuleRegistry? nativeModules = null)
    {
        NativeModules = nativeModules ?? StandardLibraryRegistry.CreateDefault();
    }

    public NativeModuleRegistry NativeModules { get; }

    /// <summary>
    /// Parses and executes one Vector source submission.
    /// </summary>
    /// <param name="source">Vector source text.</param>
    /// <param name="programRoot">
    /// Root directory used to resolve local module imports. When omitted, the current directory is used.
    /// </param>
    /// <param name="host">
    /// Optional host that also receives every output line. Output is always captured in the returned result.
    /// </param>
    public ExecutionResult Execute(
        string source,
        string? programRoot = null,
        IVectorHost? host = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var output = new List<string>();
        var executionHost = CreateExecutionHost(output, host);
        var sourceText = new SourceText(source);
        var parseResult = new Parser(sourceText).ParseCompilationUnit();

        if (parseResult.HasErrors)
        {
            return new ExecutionResult(null, parseResult.Diagnostics, output);
        }

        var root = string.IsNullOrWhiteSpace(programRoot)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(programRoot);
        var moduleLoader = new ModuleLoader(new ModuleResolver(root), NativeModules);
        var interpreter = new Interpreter(host: executionHost, moduleLoader: moduleLoader);

        try
        {
            var result = interpreter.Execute(parseResult.Root, sourceName: null, sourceText: source);
            return new ExecutionResult(result, parseResult.Diagnostics, output);
        }
        catch (RuntimeError error)
        {
            var diagnostic = new Diagnostic(
                error.Code,
                error.Message,
                DiagnosticSeverity.Error,
                error.Span,
                error.SourceName,
                error.SourceText);
            return new ExecutionResult(null, parseResult.Diagnostics.Append(diagnostic), output);
        }
        catch (ModuleLoadException error)
        {
            var diagnostics = TranslateModuleError(error);
            return new ExecutionResult(null, parseResult.Diagnostics.Concat(diagnostics), output);
        }
    }


    private static IVectorHost CreateExecutionHost(List<string> output, IVectorHost? forward) =>
        forward is IVectorInputHost inputHost
            ? new CapturingInputHost(output, inputHost)
            : new CapturingHost(output, forward);

    private static IReadOnlyList<Diagnostic> TranslateModuleError(ModuleLoadException error)
    {
        if (error.Kind == ModuleLoadErrorKind.InvalidSyntax && error.Diagnostics.Count > 0)
        {
            return error.Diagnostics;
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
        return new[]
        {
            new Diagnostic(
                code,
                error.Message,
                DiagnosticSeverity.Error,
                new SourceSpan(position, position))
        };
    }

    private class CapturingHost : IVectorHost
    {
        private readonly List<string> _output;
        private readonly IVectorHost? _forward;

        public CapturingHost(List<string> output, IVectorHost? forward)
        {
            _output = output;
            _forward = forward;
        }

        public void WriteLine(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            _output.Add(text);
            _forward?.WriteLine(text);
        }
    }

    private sealed class CapturingInputHost : CapturingHost, IVectorInputHost
    {
        private readonly IVectorInputHost _input;

        public CapturingInputHost(List<string> output, IVectorInputHost input)
            : base(output, input)
        {
            _input = input;
        }

        public string? ReadLine() => _input.ReadLine();
    }
}
