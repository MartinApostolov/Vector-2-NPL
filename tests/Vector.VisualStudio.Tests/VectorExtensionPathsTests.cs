namespace Vector.VisualStudio.Tests;

using Vector.VisualStudio.Packaging;
using Xunit;

public sealed class VectorExtensionPathsTests
{
    [Fact]
    public void ChildExecutables_AreResolvedFromExtensionAssemblyNotServiceHubBase()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vector-path-resolution-{Guid.NewGuid():N}");
        string serviceHubBase = Path.Combine(
            root,
            "VisualStudio",
            "Common7",
            "ServiceHub",
            "Hosts",
            "ServiceHub.Host.Extensibility.amd64");
        string extensionDirectory = Path.Combine(root, "Extensions", "installed-id", "OutOfProc");
        string extensionAssembly = Path.Combine(extensionDirectory, ".", "Vector.VisualStudio.dll");

        var paths = new VectorExtensionPaths(extensionAssembly);

        Assert.Equal(Path.GetFullPath(extensionAssembly), paths.ExtensionAssemblyPath);
        Assert.Equal(Path.GetFullPath(extensionDirectory), paths.ExtensionDirectory);
        Assert.Equal(
            Path.Combine(Path.GetFullPath(extensionDirectory), "LanguageServer", "Vector.LanguageServer.exe"),
            paths.LanguageServerPath);
        Assert.Equal(
            Path.Combine(Path.GetFullPath(extensionDirectory), "ExecutionHost", "Vector.ExecutionHost.exe"),
            paths.ExecutionHostPath);
        Assert.False(paths.LanguageServerPath.StartsWith(serviceHubBase, StringComparison.OrdinalIgnoreCase));
        Assert.False(paths.ExecutionHostPath.StartsWith(serviceHubBase, StringComparison.OrdinalIgnoreCase));
    }
}
