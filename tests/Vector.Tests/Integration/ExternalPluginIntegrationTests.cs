using Vector.Cli;
using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.StandardLibrary;
using Vector.Plugins;
using Vector.Plugins.Loading;
using Vector.Tests.Plugins;
using Xunit;

namespace Vector.Tests.Integration;

public sealed class ExternalPluginIntegrationTests
{
    [Fact]
    public void ExternalPluginCanExportValuesFunctionsAndSeveralModules()
    {
        var runtime = VectorPluginRuntime.CreateDefault(AcceptancePlugin());

        var result = runtime.Execute(
            "import accept.math;\n" +
            "import accept.text;\n" +
            "print(accept.math.answer);\n" +
            "print(accept.math.double(21));\n" +
            "print(accept.text.greet(\"Vector\"));");

        Assert.True(result.Success);
        Assert.Equal(new[] { "42", "42", "Hello, Vector!" }, result.Output);
        var registration = Assert.Single(runtime.Plugins.Registrations);
        Assert.Equal("fixture.acceptance", registration.Id);
        Assert.Equal(3, registration.ModuleIds.Count);
    }

    [Fact]
    public void SeveralPluginsAndStandardLibraryShareOneRuntime()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            AcceptancePlugin(),
            Plugin("Vector.TestPlugin.Second", "Vector.TestPlugin.Second.dll"));

        var result = runtime.Execute(
            "import accept.math;\n" +
            "import fixture.extra;\n" +
            "import lib.math;\n" +
            "print(accept.math.double(lib.math.sqrt(441)) + fixture.extra.increment(20));");

        Assert.True(result.Success);
        Assert.Equal("63", Assert.Single(result.Output));
        Assert.Equal(2, runtime.Plugins.Registrations.Count);
    }

    [Fact]
    public void LocalSourceModuleCanImportAndCallPluginModule()
    {
        using var program = new TemporaryProgram("vector-plugin-local-bridge");
        program.WriteModule(
            "local.bridge",
            "import accept.math;\n" +
            "function calculate(value) { return accept.math.double(value) + 1; }");
        var runtime = VectorPluginRuntime.CreateDefault(AcceptancePlugin());

        var result = runtime.Execute(
            "import local.bridge;\n" +
            "import accept.text;\n" +
            "print(local.bridge.calculate(20));\n" +
            "print(accept.text.greet(\"Local\"));",
            program.Root);

        Assert.True(result.Success);
        Assert.Equal(new[] { "41", "Hello, Local!" }, result.Output);
    }

    [Fact]
    public void LocalSourceModuleConflictWithPluginModuleRemainsExplicit()
    {
        using var program = new TemporaryProgram("vector-plugin-source-conflict");
        program.WriteModule("accept.math", "let sourceValue = 1;");
        var runtime = VectorPluginRuntime.CreateDefault(AcceptancePlugin());

        var result = runtime.Execute("import accept.math;", program.Root);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ModuleConflict, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void UserFunctionCanUsePluginResultsInArithmeticAndLists()
    {
        var runtime = VectorPluginRuntime.CreateDefault(AcceptancePlugin());

        var result = runtime.Execute(
            "import accept.math;\n" +
            "function twice(value) { return accept.math.double(value); }\n" +
            "let values = [twice(5), accept.math.answer];\n" +
            "print(values[0] + values[1]);");

        Assert.True(result.Success);
        Assert.Equal("52", Assert.Single(result.Output));
    }

    [Fact]
    public void PluginWithLocalManagedDependencyWorksThroughFullRuntime()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));

        var result = runtime.Execute(
            "import fixture.tools;\nprint(fixture.tools.double(21));");

        Assert.True(result.Success);
        Assert.Equal("42", Assert.Single(result.Output));
    }

    [Fact]
    public void CliFileExecutionUsesExplicitExternalPlugin()
    {
        using var program = new TemporaryProgram("vector-plugin-cli-acceptance");
        var source = program.WriteFile(
            "main.vec",
            "import accept.math;\nprint(accept.math.double(21));");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(
            new[] { "--plugin", AcceptancePlugin(), source },
            new StringReader(string.Empty),
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Contains("42", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void ReplKeepsExplicitExternalPluginAcrossSubmissions()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(
            new[] { "--plugin", AcceptancePlugin() },
            new StringReader(
                "import accept.math;\n" +
                "accept.math.answer;\n" +
                "accept.math.double(21);\n" +
                ":exit\n"),
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Contains("42", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void WrongPluginApiVersionLeavesManagerUnchanged()
    {
        var manager = new VectorPluginManager(StandardLibraryRegistry.CreateDefault());

        var error = Assert.Throws<VectorPluginException>(() =>
            manager.LoadFromPath(Plugin("Vector.TestPlugin.ApiMismatch", "Vector.TestPlugin.ApiMismatch.dll")));

        Assert.Equal(VectorPluginErrorKind.ApiVersionMismatch, error.ErrorKind);
        Assert.Empty(manager.Registrations);
    }

    [Theory]
    [InlineData("Vector.TestPlugin.NoEntry", "Vector.TestPlugin.NoEntry.dll", VectorPluginLoadErrorKind.NoPluginEntryPoint)]
    [InlineData("Vector.TestPlugin.MultipleEntries", "Vector.TestPlugin.MultipleEntries.dll", VectorPluginLoadErrorKind.MultiplePluginEntryPoints)]
    public void InvalidEntryPointCountsFailWithStructuredLoadErrors(
        string projectName,
        string assemblyName,
        VectorPluginLoadErrorKind expectedKind)
    {
        var manager = new VectorPluginManager(new NativeModuleRegistry());

        var error = Assert.Throws<VectorPluginLoadException>(() =>
            manager.LoadFromPath(Plugin(projectName, assemblyName)));

        Assert.Equal(expectedKind, error.ErrorKind);
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void MissingPluginDllIsStructuredLoadFailure()
    {
        var manager = new VectorPluginManager(new NativeModuleRegistry());
        var missing = Path.Combine(Path.GetTempPath(), $"vector-plugin-missing-{Guid.NewGuid():N}.dll");

        var error = Assert.Throws<VectorPluginLoadException>(() => manager.LoadFromPath(missing));

        Assert.Equal(VectorPluginLoadErrorKind.FileNotFound, error.ErrorKind);
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void MalformedPluginDllIsStructuredLoadFailure()
    {
        var manager = new VectorPluginManager(new NativeModuleRegistry());
        var malformed = Path.Combine(Path.GetTempPath(), $"vector-plugin-malformed-{Guid.NewGuid():N}.dll");
        File.WriteAllText(malformed, "not a managed assembly");

        try
        {
            var error = Assert.Throws<VectorPluginLoadException>(() => manager.LoadFromPath(malformed));

            Assert.Equal(VectorPluginLoadErrorKind.AssemblyLoadFailure, error.ErrorKind);
            Assert.Empty(manager.Registrations);
        }
        finally
        {
            File.Delete(malformed);
        }
    }

    [Fact]
    public void PluginConstructorFailureIsStructuredAndRegistersNothing()
    {
        var manager = new VectorPluginManager(new NativeModuleRegistry());

        var error = Assert.Throws<VectorPluginLoadException>(() =>
            manager.LoadFromPath(Plugin(
                "Vector.TestPlugin.ThrowingConstructor",
                "Vector.TestPlugin.ThrowingConstructor.dll")));

        Assert.Equal(VectorPluginLoadErrorKind.ConstructorFailure, error.ErrorKind);
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void MissingManagedDependencyIsStructuredAndRegistersNothing()
    {
        var sourcePlugin = Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll");
        var sourceDirectory = Path.GetDirectoryName(sourcePlugin)!;
        using var copied = new TemporaryProgram("vector-plugin-missing-managed-dependency");
        var copiedPlugin = Path.Combine(copied.Root, Path.GetFileName(sourcePlugin));
        File.Copy(sourcePlugin, copiedPlugin);

        var dependencyManifest = Path.Combine(sourceDirectory, "Vector.TestPlugin.deps.json");
        if (File.Exists(dependencyManifest))
        {
            File.Copy(
                dependencyManifest,
                Path.Combine(copied.Root, Path.GetFileName(dependencyManifest)));
        }

        var manager = new VectorPluginManager(new NativeModuleRegistry());

        var error = Assert.Throws<VectorPluginLoadException>(() => manager.LoadFromPath(copiedPlugin));

        Assert.Equal(VectorPluginLoadErrorKind.AssemblyLoadFailure, error.ErrorKind);
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void DuplicatePluginIdIsRejectedWithoutDisturbingFirstPlugin()
    {
        var manager = new VectorPluginManager(new NativeModuleRegistry());
        var plugin = AcceptancePlugin();
        manager.LoadFromPath(plugin);

        var error = Assert.Throws<VectorPluginException>(() => manager.LoadFromPath(plugin));

        Assert.Equal(VectorPluginErrorKind.DuplicatePlugin, error.ErrorKind);
        Assert.Single(manager.Registrations);
        Assert.Contains(manager.Registrations[0].ModuleIds, id => id.QualifiedName == "accept.math");
    }

    [Fact]
    public void DuplicateModuleInsideExternalPluginIsRejectedTransactionally()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);

        var error = Assert.Throws<VectorPluginException>(() =>
            manager.LoadFromPath(Plugin(
                "Vector.TestPlugin.DuplicateModule",
                "Vector.TestPlugin.DuplicateModule.dll")));

        Assert.Equal(VectorPluginErrorKind.DuplicateModule, error.ErrorKind);
        Assert.False(registry.TryGet(Id("accept.duplicate"), out _));
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void PluginCannotReplaceStandardLibraryModule()
    {
        var runtime = VectorPluginRuntime.CreateDefault();

        var error = Assert.Throws<VectorPluginException>(() =>
            runtime.Plugins.LoadFromPath(Plugin(
                "Vector.TestPlugin.StandardConflict",
                "Vector.TestPlugin.StandardConflict.dll")));

        Assert.Equal(VectorPluginErrorKind.ModuleConflict, error.ErrorKind);
        Assert.Empty(runtime.Plugins.Registrations);
        Assert.True(runtime.NativeModules.TryGet(Id("lib.math"), out _));
    }

    [Fact]
    public void PluginVsPluginModuleConflictCommitsNothingFromRejectedPlugin()
    {
        var runtime = VectorPluginRuntime.CreateDefault(AcceptancePlugin());

        var error = Assert.Throws<VectorPluginException>(() =>
            runtime.Plugins.LoadFromPath(Plugin(
                "Vector.TestPlugin.ModuleConflict",
                "Vector.TestPlugin.ModuleConflict.dll")));

        Assert.Equal(VectorPluginErrorKind.ModuleConflict, error.ErrorKind);
        Assert.Equal("accept.math", error.ModuleId?.QualifiedName);
        Assert.False(runtime.NativeModules.TryGet(Id("accept.safe"), out _));
        Assert.Single(runtime.Plugins.Registrations);
    }

    [Fact]
    public void RegistrationExceptionLeavesNoPartialModules()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);

        var error = Assert.Throws<VectorPluginException>(() =>
            manager.LoadFromPath(Plugin(
                "Vector.TestPlugin.RegistrationFailure",
                "Vector.TestPlugin.RegistrationFailure.dll")));

        Assert.Equal(VectorPluginErrorKind.RegistrationFailure, error.ErrorKind);
        Assert.False(registry.TryGet(Id("fixture.staged"), out _));
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void DeliberateNativePluginFailureBecomesVectorDiagnosticAtRuntime()
    {
        var runtime = VectorPluginRuntime.CreateDefault(AcceptancePlugin());

        var result = runtime.Execute(
            "import accept.errors;\naccept.errors.explicitFailure();");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, diagnostic.Code);
        Assert.Contains("deliberate failure", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnexpectedPluginExceptionBecomesSafeVectorDiagnostic()
    {
        var runtime = VectorPluginRuntime.CreateDefault(AcceptancePlugin());

        var result = runtime.Execute(
            "import accept.errors;\naccept.errors.unexpectedFailure();");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Contains("unexpectedFailure", diagnostic.Message);
        Assert.DoesNotContain("secret", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", diagnostic.Message);
    }

    [Fact]
    public void InvalidNullPluginReturnBecomesSafeVectorDiagnostic()
    {
        var runtime = VectorPluginRuntime.CreateDefault(AcceptancePlugin());

        var result = runtime.Execute(
            "import accept.errors;\naccept.errors.invalidNull();");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Contains("null", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PluginDllInProgramDirectoryIsNotAutoScanned()
    {
        using var program = new TemporaryProgram("vector-plugin-no-autoscan");
        File.Copy(
            AcceptancePlugin(),
            Path.Combine(program.Root, "Vector.TestPlugin.Acceptance.dll"));
        var runtime = VectorPluginRuntime.CreateDefault();

        var result = runtime.Execute("import accept.math;", program.Root);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ModuleNotFound, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void PublicCSharpMethodsAreNotExposedUnlessRegistered()
    {
        var runtime = VectorPluginRuntime.CreateDefault(AcceptancePlugin());

        var result = runtime.Execute(
            "import accept.math;\naccept.math.Unregistered(1);");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void VectorSourceHasNoBuiltinDllLoadingFunction()
    {
        var result = VectorPluginRuntime.CreateDefault().Execute("loadPlugin(\"plugin.dll\");");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    private static string AcceptancePlugin() =>
        Plugin("Vector.TestPlugin.Acceptance", "Vector.TestPlugin.Acceptance.dll");

    private static string Plugin(string projectName, string assemblyName) =>
        PluginFixture.Assembly(projectName, assemblyName);

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram(string prefix)
        {
            Root = Path.Combine(Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string WriteModule(string qualifiedName, string source)
        {
            var relativePath = qualifiedName.Replace('.', Path.DirectorySeparatorChar) + ".vec";
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source);
            return path;
        }

        public string WriteFile(string fileName, string source)
        {
            var path = Path.Combine(Root, fileName);
            File.WriteAllText(path, source);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // Non-collectible plugin contexts can keep copied DLLs mapped on Windows.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup only.
            }
        }
    }
}
