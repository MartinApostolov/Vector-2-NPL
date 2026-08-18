using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Tests.Integration;
using Xunit;

namespace Vector.Tests.Modules;

public sealed class VmModuleCompatibilityTests
{
    [Fact]
    public void LocalModuleGraphStateAndImportedFunctionBehaviorMatch()
    {
        using var program = new TemporaryProgram();
        program.Write("counter", "let count = 0; function next() { count = count + 1; return count; }");
        program.Write("feature", "import counter; import lib.math; function run() { return [counter.next(), counter.next(), lib.math.sqrt(81)]; }");

        CompatibilityAssert.Success("import feature; feature.run();", program.Root);
    }

    [Fact]
    public void ModuleNotFoundAndCircularImportDiagnosticsMatch()
    {
        using var missing = new TemporaryProgram();
        CompatibilityAssert.Failure("import missing.module;", missing.Root);

        using var circular = new TemporaryProgram();
        circular.Write("a", "import b;");
        circular.Write("b", "import a;");
        var vm = new VectorVmEngine().Execute("import a;", circular.Root);
        var interpreter = new VectorEngine().Execute("import a;", circular.Root);
        CompatibilityAssert.Equivalent(interpreter, vm);
        Assert.Equal(DiagnosticCode.CircularImport, Assert.Single(vm.Diagnostics).Code);
    }

    [Fact]
    public void ImportedSyntaxRuntimeAndFunctionFailuresKeepSourceInformation()
    {
        using var syntax = new TemporaryProgram();
        syntax.Write("broken", "let value = ;");
        CompatibilityAssert.Failure("import broken;", syntax.Root);

        using var runtime = new TemporaryProgram();
        var modulePath = runtime.Write("broken", "function fail() { return 1 / 0; }");
        var interpreter = new VectorEngine().Execute("import broken; broken.fail();", runtime.Root);
        var vm = new VectorVmEngine().Execute("import broken; broken.fail();", runtime.Root);
        CompatibilityAssert.Equivalent(interpreter, vm);
        Assert.Equal(Path.GetFullPath(modulePath), Assert.Single(vm.Diagnostics).SourceName);
    }

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"vector-vm-compat-modules-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string Write(string qualifiedName, string source)
        {
            var relative = qualifiedName.Replace('.', Path.DirectorySeparatorChar) + ".vec";
            var path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source);
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
