using Vector.Core.Bytecode.Vm;
using Vector.Core.Diagnostics;
using Vector.Core.Execution;
using Vector.Core.Modules;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;
using Vector.Core.Source;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Core;

/// <summary>
/// Persistent bytecode-VM execution context for interactive or incremental hosts.
/// Each submission is compiled independently while variables, closures, imports, and module state persist.
/// </summary>
public sealed class VectorVmSession
{
    private readonly VectorVmEngine _engine;
    private readonly List<string> _capturedOutput = new();
    private readonly VectorVirtualMachine _virtualMachine;

    internal VectorVmSession(
        VectorVmEngine engine,
        string? programRoot,
        IVectorHost? host)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        var root = string.IsNullOrWhiteSpace(programRoot)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(programRoot);
        var executionHost = CreateExecutionHost(_capturedOutput, host);
        var moduleLoader = new ModuleLoader(
            new ModuleResolver(root),
            engine.NativeModules,
            new BytecodeSourceModuleExecutor());
        var rootEnvironment = new RuntimeEnvironment();
        _virtualMachine = new VectorVirtualMachine(rootEnvironment, executionHost, moduleLoader);
    }

    /// <summary>
    /// Compiles and executes one source submission while retaining session state for later submissions.
    /// </summary>
    public ExecutionResult Execute(string source, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        _capturedOutput.Clear();
        var compilation = _engine.Compile(source, sourceName);
        if (!compilation.Success || compilation.Program is null)
        {
            return new ExecutionResult(
                null,
                compilation.Diagnostics,
                _capturedOutput);
        }

        try
        {
            var result = _virtualMachine.Execute(compilation.Program).Result;
            return new ExecutionResult(
                result,
                compilation.Diagnostics,
                _capturedOutput);
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
            return new ExecutionResult(
                null,
                compilation.Diagnostics.Append(diagnostic),
                _capturedOutput);
        }
        catch (ModuleLoadException error)
        {
            return new ExecutionResult(
                null,
                compilation.Diagnostics.Concat(TranslateModuleError(error)),
                _capturedOutput);
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
