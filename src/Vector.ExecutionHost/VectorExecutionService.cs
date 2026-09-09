namespace Vector.ExecutionHost;

using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Execution;
using Vector.Core.Runtime;
using Vector.ExecutionProtocol;
using Vector.Plugins;
using Vector.Plugins.Loading;

public sealed class VectorExecutionService
{
    public VectorExecutionResponse Execute(VectorExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            VectorHostFailure? validationFailure = Validate(request);
            if (validationFailure is not null)
            {
                return Failure(request, validationFailure);
            }

            string root = ResolveProgramRoot(request);
            if (request.Operation == VectorExecutionOperation.Disassemble)
            {
                return Disassemble(request);
            }

            VectorPluginRuntime runtime = VectorPluginRuntime.CreateDefault();
            foreach (string pluginPath in request.PluginPaths)
            {
                runtime.Plugins.LoadFromPath(pluginPath);
            }

            ExecutionResult result = request.Engine switch
            {
                VectorExecutionEngine.Interpreter => runtime.Execute(request.Source, root),
                VectorExecutionEngine.Vm => runtime.ExecuteVm(request.Source, root),
                _ => throw new InvalidOperationException($"Unsupported execution engine '{request.Engine}'."),
            };
            return new VectorExecutionResponse
            {
                Success = result.Success,
                Engine = request.Engine,
                Operation = request.Operation,
                Output = result.Output.ToArray(),
                Result = result.Result is null ? null : VectorValueFormatter.Format(result.Result),
                Diagnostics = MapDiagnostics(result.Diagnostics, request.SourcePath),
            };
        }
        catch (VectorPluginLoadException error)
        {
            return Failure(request, new VectorHostFailure("plugin_load_failed", error.Message));
        }
        catch (VectorPluginException error)
        {
            return Failure(request, new VectorHostFailure("plugin_registration_failed", error.Message));
        }
        catch (Exception error) when (error is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or PathTooLongException)
        {
            return Failure(request, new VectorHostFailure("invalid_request", error.Message));
        }
        catch (Exception error)
        {
            return Failure(
                request,
                new VectorHostFailure("host_failure", $"{error.GetType().Name}: {error.Message}"));
        }
    }

    private static VectorExecutionResponse Disassemble(VectorExecutionRequest request)
    {
        VmCompilationResult compilation = new VectorVmEngine().Compile(request.Source, request.SourcePath);
        return new VectorExecutionResponse
        {
            Success = compilation.Success,
            Engine = request.Engine,
            Operation = request.Operation,
            Diagnostics = MapDiagnostics(compilation.Diagnostics, request.SourcePath),
            Disassembly = compilation.Disassembly,
        };
    }

    private static VectorHostFailure? Validate(VectorExecutionRequest request)
    {
        if (request.Source is null)
        {
            return new VectorHostFailure("invalid_request", "The source text is required.");
        }

        if (request.SourcePath is not null && string.IsNullOrWhiteSpace(request.SourcePath))
        {
            return new VectorHostFailure("invalid_request", "The source path cannot be empty.");
        }

        if (request.ProgramRoot is not null && string.IsNullOrWhiteSpace(request.ProgramRoot))
        {
            return new VectorHostFailure("invalid_request", "The program root cannot be empty.");
        }

        if (request.PluginPaths is null || request.PluginPaths.Any(string.IsNullOrWhiteSpace))
        {
            return new VectorHostFailure("invalid_request", "Plugin paths must be non-empty strings.");
        }

        if (request.Operation == VectorExecutionOperation.Disassemble && request.PluginPaths.Length > 0)
        {
            return new VectorHostFailure(
                "plugins_not_allowed",
                "Plugin loading is allowed only for explicit run operations.");
        }

        return null;
    }

    private static string ResolveProgramRoot(VectorExecutionRequest request)
    {
        string root;
        if (request.ProgramRoot is not null)
        {
            root = request.ProgramRoot;
        }
        else if (request.SourcePath is not null)
        {
            root = Path.GetDirectoryName(Path.GetFullPath(request.SourcePath))
                ?? Directory.GetCurrentDirectory();
        }
        else
        {
            root = Directory.GetCurrentDirectory();
        }

        string normalizedRoot = Path.GetFullPath(root);
        if (request.ProgramRoot is not null && !Directory.Exists(normalizedRoot))
        {
            throw new DirectoryNotFoundException($"The Vector program root does not exist: '{normalizedRoot}'.");
        }

        return normalizedRoot;
    }

    private static VectorExecutionResponse Failure(VectorExecutionRequest request, VectorHostFailure failure) => new()
    {
        Success = false,
        Engine = request.Engine,
        Operation = request.Operation,
        HostFailure = failure,
    };

    private static VectorProtocolDiagnostic[] MapDiagnostics(
        IEnumerable<Diagnostic> diagnostics,
        string? fallbackSourceName) => diagnostics.Select(diagnostic => new VectorProtocolDiagnostic(
            diagnostic.Code.ToString(),
            diagnostic.Severity.ToString().ToLowerInvariant(),
            diagnostic.Message,
            diagnostic.SourceName ?? fallbackSourceName,
            new VectorProtocolRange(ToPosition(diagnostic.Span.Start), ToPosition(diagnostic.Span.End))))
        .ToArray();

    private static VectorProtocolPosition ToPosition(Vector.Core.Source.SourcePosition position) =>
        new(position.Offset, Math.Max(0, position.Line - 1), Math.Max(0, position.Column - 1));
}
