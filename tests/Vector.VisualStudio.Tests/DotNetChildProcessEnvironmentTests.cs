namespace Vector.VisualStudio.Tests;

using System.Diagnostics;
using Vector.VisualStudio.Diagnostics;
using Xunit;

public sealed class DotNetChildProcessEnvironmentTests
{
    [Fact]
    public void UseMachineWideRuntime_ReplacesVisualStudioPrivateRuntimeOverrides()
    {
        var startInfo = new ProcessStartInfo();
        startInfo.Environment["DOTNET_ROOT"] = @"C:\Program Files\Microsoft Visual Studio\18\Community\dotnet\net10.0\runtime";
        startInfo.Environment["DOTNET_ROOT_X64"] = @"C:\private-dotnet";

        string description = DotNetChildProcessEnvironment.UseMachineWideRuntime(startInfo);

        Assert.Contains("inheritedRuntimeOverrides=", description, StringComparison.Ordinal);
        Assert.Contains("net10.0", description, StringComparison.Ordinal);
        Assert.False(
            startInfo.Environment.TryGetValue("DOTNET_ROOT", out string? root)
            && root?.Contains("net10.0", StringComparison.OrdinalIgnoreCase) == true);
        Assert.False(
            startInfo.Environment.TryGetValue("DOTNET_ROOT_X64", out string? x64Root)
            && x64Root?.Contains("private-dotnet", StringComparison.OrdinalIgnoreCase) == true);
    }
}
