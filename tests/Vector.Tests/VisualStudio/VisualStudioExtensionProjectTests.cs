using System.Text.Json;
using Xunit;

namespace Vector.Tests.VisualStudio;

public sealed class VisualStudioExtensionProjectTests
{
    private const string ExtensionProjectPath = "src/Vector.VisualStudio/Vector.VisualStudio.csproj";
    private const string PackageProjectPath = "src/Vector.VisualStudio.Package/Vector.VisualStudio.Package.csproj";
    private const string ExtensionSourcePath = "src/Vector.VisualStudio/VectorExtension.cs";
    private const string DocumentTypeSourcePath = "src/Vector.VisualStudio/VectorDocumentType.cs";
    private const string LanguageServerProviderSourcePath = "src/Vector.VisualStudio/VectorLanguageServerProvider.cs";
    private const string AboutCommandSourcePath = "src/Vector.VisualStudio/AboutVectorExtensionCommand.cs";
    private const string StringResourcesPath = "src/Vector.VisualStudio/.vsextension/string-resources.json";

    [Fact]
    public void Solution_ContainsVisualStudioExtensionProject()
    {
        var root = FindRepositoryRoot();
        var solution = File.ReadAllText(Path.Combine(root, "Vector.sln"));

        Assert.Contains(@"src\Vector.VisualStudio\Vector.VisualStudio.csproj", solution, StringComparison.Ordinal);
        Assert.Contains(@"src\Vector.VisualStudio.Package\Vector.VisualStudio.Package.csproj", solution, StringComparison.Ordinal);
        Assert.Contains("{CC2016D1-1A14-45B7-A4E3-0C6E58E99C18}.Debug|Any CPU.Deploy.0 = Debug|Any CPU", solution, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtensionProject_TargetsNet8WindowsAndPinnedExtensibilitySdk()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, ExtensionProjectPath));

        Assert.Contains("<TargetFramework>net8.0-windows8.0</TargetFramework>", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.VisualStudio.Extensibility.Sdk\" Version=\"17.14.40608\"", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.VisualStudio.Extensibility.Build\" Version=\"17.14.40608\"", project, StringComparison.Ordinal);
        Assert.Contains("<AssemblyVSIXSubPath>OutOfProc</AssemblyVSIXSubPath>", project, StringComparison.Ordinal);
        Assert.Contains("<CreateVsixContainer>false</CreateVsixContainer>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Vector.Core", project, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageProject_IsSeparateTraditionalContainerForOutOfProcessExtension()
    {
        var root = FindRepositoryRoot();
        string packageProject = File.ReadAllText(Path.Combine(root, PackageProjectPath));

        Assert.Contains("<TargetFramework>net472</TargetFramework>", packageProject, StringComparison.Ordinal);
        Assert.Contains("<VssdkCompatibleExtension>true</VssdkCompatibleExtension>", packageProject, StringComparison.Ordinal);
        Assert.Contains("<ReferenceOutputAssembly>false</ReferenceOutputAssembly>", packageProject, StringComparison.Ordinal);
        Assert.Contains("<SkipGetTargetFrameworkProperties>true</SkipGetTargetFrameworkProperties>", packageProject, StringComparison.Ordinal);
        Assert.Contains("<IncludeOutputGroupsInVSIX>ExtensionFilesOutputGroup</IncludeOutputGroupsInVSIX>", packageProject, StringComparison.Ordinal);
        Assert.Contains("Vector.LanguageConfiguration.pkgdef", packageProject, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtensionMetadata_UsesStableUniqueIdentity()
    {
        var root = FindRepositoryRoot();
        var extensionSource = File.ReadAllText(Path.Combine(root, ExtensionSourcePath));

        Assert.Contains("Vector.VisualStudio.7e4c1e9c-7699-48fd-b73f-d9fc5ef24ec3", extensionSource, StringComparison.Ordinal);
        Assert.Contains("displayName: \"Vector Language Support\"", extensionSource, StringComparison.Ordinal);
        Assert.Contains("publisherName: \"Martin Apostolov\"", extensionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VectorDocumentType_RegistersVecForLanguageServer()
    {
        var root = FindRepositoryRoot();
        var documentTypeSource = File.ReadAllText(Path.Combine(root, DocumentTypeSourcePath));

        Assert.Contains("VectorFileExtension = \".vec\"", documentTypeSource, StringComparison.Ordinal);
        Assert.Contains("LanguageServerBaseDocumentType", documentTypeSource, StringComparison.Ordinal);
        Assert.Contains("[VisualStudioContribution]", documentTypeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AboutCommand_IsNonExecutingToolsMenuSmokeCommand()
    {
        var root = FindRepositoryRoot();
        var commandSource = File.ReadAllText(Path.Combine(root, AboutCommandSourcePath));

        Assert.Contains("CommandPlacement.KnownPlacements.ToolsMenu", commandSource, StringComparison.Ordinal);
        Assert.Contains("ShowPromptAsync", commandSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VectorEngine", commandSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", commandSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Commit 75", commandSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionCommands_InitializeOutputAndSurfaceUnexpectedFailures()
    {
        var root = FindRepositoryRoot();
        string commandSource = File.ReadAllText(Path.Combine(
            root,
            "src/Vector.VisualStudio/Commands/VectorExecutionCommand.cs"));

        Assert.Contains("override async Task InitializeAsync", commandSource, StringComparison.Ordinal);
        Assert.Contains("output.InitializeAsync", commandSource, StringComparison.Ordinal);
        Assert.Contains("GetActiveTextViewAsync", commandSource, StringComparison.Ordinal);
        Assert.Contains("Document.Text.CopyToString()", commandSource, StringComparison.Ordinal);
        Assert.Contains("catch (Exception error)", commandSource, StringComparison.Ordinal);
        Assert.Contains("ShowPromptAsync", commandSource, StringComparison.Ordinal);
    }

    [Fact]
    public void StringResources_AreValidAndContainAboutCommandStrings()
    {
        var root = FindRepositoryRoot();
        var json = File.ReadAllText(Path.Combine(root, StringResourcesPath));
        using var document = JsonDocument.Parse(json);

        var rootElement = document.RootElement;
        Assert.Equal("Vector: About Vector Extension", rootElement.GetProperty("Vector.VisualStudio.About.DisplayName").GetString());
        Assert.False(string.IsNullOrWhiteSpace(rootElement.GetProperty("Vector.VisualStudio.About.Tooltip").GetString()));
        Assert.Equal("Vector Language Server", rootElement.GetProperty("Vector.VisualStudio.LanguageServer.DisplayName").GetString());
    }

    [Fact]
    public void ExtensionProject_PackagesSeparateLanguageServer()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, ExtensionProjectPath));

        Assert.Contains("Vector.LanguageServer\\Vector.LanguageServer.csproj", project, StringComparison.Ordinal);
        Assert.Contains("IncludeVectorLanguageServerInVsix", project, StringComparison.Ordinal);
        Assert.Contains("<VSIXSubPath>OutOfProc\\LanguageServer</VSIXSubPath>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void LanguageServerProvider_IsBoundToVectorDocumentsAndUsesStdio()
    {
        var root = FindRepositoryRoot();
        var provider = File.ReadAllText(Path.Combine(root, LanguageServerProviderSourcePath));

        Assert.Contains("DocumentFilter.FromDocumentType(VectorExtension.VectorDocumentType)", provider, StringComparison.Ordinal);
        Assert.Contains("RedirectStandardInput = true", provider, StringComparison.Ordinal);
        Assert.Contains("RedirectStandardOutput = true", provider, StringComparison.Ordinal);
        Assert.Contains("Kill(entireProcessTree: true)", provider, StringComparison.Ordinal);
        Assert.Contains("TraceSource traceSource", provider, StringComparison.Ordinal);
        Assert.Contains("beforeInitializeCompleted", provider, StringComparison.Ordinal);
        Assert.Contains("Settings restart deferred", provider, StringComparison.Ordinal);
        Assert.Contains("StopServer() called", provider, StringComparison.Ordinal);
        Assert.Contains("VectorExtensionPaths", provider, StringComparison.Ordinal);
        Assert.Contains("this.extensionPaths.LanguageServerPath", provider, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.GetFullPath(AppContext.BaseDirectory)", provider, StringComparison.Ordinal);
        Assert.Contains("DotNetChildProcessEnvironment.UseMachineWideRuntime", provider, StringComparison.Ordinal);

        string executionClient = File.ReadAllText(Path.Combine(
            root,
            "src/Vector.VisualStudio/Execution/VectorExecutionClient.cs"));
        Assert.Contains("new VectorExtensionPaths().ExecutionHostPath", executionClient, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.Combine(AppContext.BaseDirectory", executionClient, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vector.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Vector repository root from the test output directory.");
    }
}
