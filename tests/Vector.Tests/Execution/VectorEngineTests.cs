using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Execution;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Xunit;

namespace Vector.Tests.Execution;

public sealed class VectorEngineTests
{
    [Fact]
    public void ExecuteReturnsFinalProgramValue()
    {
        var result = new VectorEngine().Execute("let value = 20; value + 5;");

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new NumberValue(25), result.Result);
    }

    [Fact]
    public void EmptySourceReturnsNothing()
    {
        var result = new VectorEngine().Execute(string.Empty);

        Assert.True(result.Success);
        Assert.Same(NothingValue.Instance, result.Result);
        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.Output);
    }

    [Fact]
    public void ExecuteCapturesPrintOutput()
    {
        var result = new VectorEngine().Execute("print(\"hello\"); print(42);");

        Assert.True(result.Success);
        Assert.Equal(new[] { "hello", "42" }, result.Output);
    }

    [Fact]
    public void ExecuteForwardsOutputToProvidedHostAndStillCapturesIt()
    {
        var forwarded = new List<string>();
        var host = new VectorHost(forwarded.Add);

        var result = new VectorEngine().Execute("print(\"one\"); print(\"two\");", host: host);

        Assert.True(result.Success);
        Assert.Equal(new[] { "one", "two" }, result.Output);
        Assert.Equal(result.Output, forwarded);
    }

    [Fact]
    public void LexerFailureIsReturnedAsDiagnosticWithoutExecutingProgram()
    {
        var result = new VectorEngine().Execute("print(\"should not run\"); @;");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.InvalidCharacter);
        Assert.Null(result.Result);
        Assert.Empty(result.Output);
    }

    [Fact]
    public void ParserFailureIsReturnedAsDiagnosticWithoutExecutingProgram()
    {
        var result = new VectorEngine().Execute("print(\"should not run\"); let value = ;");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Null(result.Result);
        Assert.Empty(result.Output);
    }

    [Fact]
    public void RuntimeFailureIsReturnedAsStructuredDiagnostic()
    {
        var result = new VectorEngine().Execute("1 + \"two\";");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Span.Length > 0);
        Assert.Null(result.Result);
    }

    [Fact]
    public void RuntimeFailurePreservesOutputProducedBeforeFailure()
    {
        var result = new VectorEngine().Execute("print(\"before\"); 1 / 0;");

        Assert.False(result.Success);
        Assert.Equal(new[] { "before" }, result.Output);
        Assert.Equal(DiagnosticCode.DivisionByZero, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("missing;", DiagnosticCode.UndefinedVariable)]
    [InlineData("[1][5];", DiagnosticCode.ListIndexOutOfRange)]
    [InlineData("print();", DiagnosticCode.ArgumentCountMismatch)]
    public void RuntimeDiagnosticCodesSurviveEngineBoundary(string source, DiagnosticCode expectedCode)
    {
        var result = new VectorEngine().Execute(source);

        Assert.False(result.Success);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void EngineExecutesImportedModuleUsingExplicitProgramRoot()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.source_math", "function add(a, b) { return a + b; }");

        var result = new VectorEngine().Execute(
            "import lib.source_math; lib.source_math.add(6, 7);",
            program.Root);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(13), result.Result);
    }

    [Fact]
    public void ModuleOutputIsCapturedThroughSameExecutionHost()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("speaker", "print(\"module\"); let value = 1;");

        var result = new VectorEngine().Execute(
            "import speaker; print(\"main\"); speaker.value;",
            program.Root);

        Assert.True(result.Success);
        Assert.Equal(new[] { "module", "main" }, result.Output);
        Assert.Equal(new NumberValue(1), result.Result);
    }

    [Fact]
    public void MissingModuleIsReturnedAsStructuredDiagnostic()
    {
        using var program = new TemporaryProgram();

        var result = new VectorEngine().Execute("import missing.module;", program.Root);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.ModuleNotFound, diagnostic.Code);
        Assert.Contains("missing.module", diagnostic.Message);
    }

    [Fact]
    public void InvalidModuleSyntaxIsReturnedAsParserDiagnostic()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("broken", "let value = ;");

        var result = new VectorEngine().Execute("import broken;", program.Root);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCode.ExpectedExpression ||
            diagnostic.Code == DiagnosticCode.UnexpectedToken);
    }

    [Fact]
    public void CircularModuleImportIsReturnedAsStructuredDiagnostic()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("a", "import b;");
        program.WriteModule("b", "import a;");

        var result = new VectorEngine().Execute("import a;", program.Root);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.CircularImport, diagnostic.Code);
        Assert.Contains("a -> b -> a", diagnostic.Message);
    }

    [Fact]
    public void RuntimeFailureInsideImportedModuleIsReturnedAsDiagnostic()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("broken", "let value = 1 / 0;");

        var result = new VectorEngine().Execute("import broken;", program.Root);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.DivisionByZero, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void EngineSupportsFunctionsLoopsListsAndBuiltinsThroughOneApi()
    {
        const string source = """
            function double(value) { return value * 2; }
            let values = [];
            for number in range(1, 4) {
                values = concat(values, [double(number)]);
            }
            values;
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(2),
                new NumberValue(4),
                new NumberValue(6)
            }),
            result.Result);
    }

    [Fact]
    public void SuccessfulNothingResultIsDifferentFromFailedNullResult()
    {
        var successful = new VectorEngine().Execute("let value = 1;");
        var failed = new VectorEngine().Execute("missing;");

        Assert.True(successful.Success);
        Assert.Same(NothingValue.Instance, successful.Result);
        Assert.False(failed.Success);
        Assert.Null(failed.Result);
    }

    [Fact]
    public void ExecutionResultCopiesDiagnosticAndOutputCollections()
    {
        var diagnostics = new List<Diagnostic>();
        var output = new List<string> { "first" };
        var result = new ExecutionResult(NothingValue.Instance, diagnostics, output);

        diagnostics.Add(new Diagnostic(
            DiagnosticCode.Unspecified,
            "later",
            DiagnosticSeverity.Error,
            TestSpan()));
        output.Add("second");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new[] { "first" }, result.Output);
        Assert.True(result.Success);
    }

    [Fact]
    public void EngineDoesNotRequireCallerToConstructParserOrInterpreter()
    {
        var engine = new VectorEngine();

        var result = engine.Execute("let x = 3; let y = 4; x * y;");

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(12), result.Result);
    }

    private static Vector.Core.Source.SourceSpan TestSpan() =>
        new(
            new Vector.Core.Source.SourcePosition(0, 1, 1),
            new Vector.Core.Source.SourcePosition(0, 1, 1));

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorEngineTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void WriteModule(string qualifiedName, string source)
        {
            var segments = qualifiedName.Split('.');
            var fileName = segments[^1] + ".vec";
            var directory = segments.Length == 1
                ? Root
                : Path.Combine(new[] { Root }.Concat(segments[..^1]).ToArray());

            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), source);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
