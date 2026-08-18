using Vector.Core.Bytecode;
using Vector.Core.Bytecode.Compiler;
using Vector.Core.Bytecode.Vm;
using Vector.Core.Diagnostics;
using Vector.Core.Execution;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;
using Vector.Core.Source;
using Vector.Core.StandardLibrary;

namespace Vector.Core;

/// <summary>
/// High-level reusable entry point for compiling and executing Vector source with the bytecode VM.
/// </summary>
public sealed class VectorVmEngine
{
    public VectorVmEngine(NativeModuleRegistry? nativeModules = null)
    {
        NativeModules = nativeModules ?? StandardLibraryRegistry.CreateDefault();
    }

    /// <summary>
    /// Gets the native-module registry used by VM executions created by this engine.
    /// </summary>
    public NativeModuleRegistry NativeModules { get; }

    /// <summary>
    /// Parses and compiles Vector source into VM bytecode and exposes its deterministic disassembly.
    /// </summary>
    /// <param name="source">Vector source text.</param>
    /// <param name="sourceName">Optional display name recorded in bytecode source metadata.</param>
    public VmCompilationResult Compile(string source, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (sourceName is not null && string.IsNullOrWhiteSpace(sourceName))
        {
            throw new ArgumentException("A source name cannot be empty.", nameof(sourceName));
        }

        var sourceText = new SourceText(source);
        var parseResult = new Parser(sourceText).ParseCompilationUnit();

        if (parseResult.HasErrors)
        {
            var diagnostics = sourceName is null
                ? parseResult.Diagnostics
                : parseResult.Diagnostics
                    .Select(diagnostic => diagnostic.WithSource(sourceName, source))
                    .ToArray();
            return new VmCompilationResult(null, diagnostics, null);
        }

        var compilation = new BytecodeCompiler().Compile(parseResult.Root, sourceName, source);
        var disassembly = BytecodeDisassembler.Disassemble(compilation.Program);
        return new VmCompilationResult(compilation.Program, parseResult.Diagnostics, disassembly);
    }

    /// <summary>
    /// Parses, compiles, and executes one Vector source submission with the bytecode VM.
    /// </summary>
    /// <param name="source">Vector source text.</param>
    /// <param name="programRoot">
    /// Root directory used to resolve local module imports. When omitted, the current directory is used.
    /// </param>
    /// <param name="host">
    /// Optional host that also receives every output line. Output is always captured in the returned result.
    /// Input-capable hosts retain their input capability while output is captured.
    /// </param>
    public ExecutionResult Execute(
        string source,
        string? programRoot = null,
        IVectorHost? host = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var output = new List<string>();
        var executionHost = CreateExecutionHost(output, host);
        var compilation = Compile(source);

        if (!compilation.Success || compilation.Program is null)
        {
            return new ExecutionResult(null, compilation.Diagnostics, output);
        }

        var root = string.IsNullOrWhiteSpace(programRoot)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(programRoot);
        var moduleLoader = new ModuleLoader(
            new ModuleResolver(root),
            NativeModules,
            new BytecodeSourceModuleExecutor());
        var virtualMachine = new VectorVirtualMachine(host: executionHost, moduleLoader: moduleLoader);

        try
        {
            var result = virtualMachine.Execute(compilation.Program).Result;
            return new ExecutionResult(result, compilation.Diagnostics, output);
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
            return new ExecutionResult(null, compilation.Diagnostics.Append(diagnostic), output);
        }
        catch (ModuleLoadException error)
        {
            var diagnostics = TranslateModuleError(error);
            return new ExecutionResult(null, compilation.Diagnostics.Concat(diagnostics), output);
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
