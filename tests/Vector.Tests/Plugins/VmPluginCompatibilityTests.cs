using Vector.Core.Diagnostics;
using Vector.Plugins;
using Vector.Tests.Integration;
using Xunit;

namespace Vector.Tests.Plugins;

public sealed class VmPluginCompatibilityTests
{
    [Fact]
    public void ExternalPluginModulesMatchAcrossBackends()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            PluginFixture.Assembly("Vector.TestPlugin.Acceptance", "Vector.TestPlugin.Acceptance.dll"));
        const string source = "import accept.math; import accept.text; [accept.math.answer, accept.math.double(21), accept.text.greet(\"Vector\")];";

        CompatibilityAssert.Equivalent(runtime.Execute(source), runtime.ExecuteVm(source));
    }

    [Fact]
    public void ExplicitAndUnexpectedPluginFailuresMatchAcrossBackends()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            PluginFixture.Assembly("Vector.TestPlugin.Acceptance", "Vector.TestPlugin.Acceptance.dll"));

        var explicitInterpreter = runtime.Execute("import accept.errors; accept.errors.explicitFailure();");
        var explicitVm = runtime.ExecuteVm("import accept.errors; accept.errors.explicitFailure();");
        CompatibilityAssert.Equivalent(explicitInterpreter, explicitVm);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, Assert.Single(explicitVm.Diagnostics).Code);

        var unexpectedInterpreter = runtime.Execute("import accept.errors; accept.errors.unexpectedFailure();");
        var unexpectedVm = runtime.ExecuteVm("import accept.errors; accept.errors.unexpectedFailure();");
        CompatibilityAssert.Equivalent(unexpectedInterpreter, unexpectedVm);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, Assert.Single(unexpectedVm.Diagnostics).Code);
    }

    [Fact]
    public void SourceAndPluginModuleConflictMatches()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            PluginFixture.Assembly("Vector.TestPlugin", "Vector.TestPlugin.dll"));
        var root = Path.Combine(Path.GetTempPath(), $"vector-vm-plugin-conflict-{Guid.NewGuid():N}");
        var moduleDirectory = Path.Combine(root, "fixture");
        Directory.CreateDirectory(moduleDirectory);
        File.WriteAllText(Path.Combine(moduleDirectory, "tools.vec"), "let local = 1;");

        try
        {
            var interpreter = runtime.Execute("import fixture.tools;", root);
            var vm = runtime.ExecuteVm("import fixture.tools;", root);
            CompatibilityAssert.Equivalent(interpreter, vm);
            Assert.False(vm.Success);
            Assert.Equal(DiagnosticCode.ModuleConflict, Assert.Single(vm.Diagnostics).Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
