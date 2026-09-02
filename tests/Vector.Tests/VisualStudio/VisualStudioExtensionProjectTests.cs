using System.Text.Json;
using Xunit;

namespace Vector.Tests.VisualStudio;

public sealed class VisualStudioExtensionProjectTests
{
    private const string ExtensionProjectPath = "src/Vector.VisualStudio/Vector.VisualStudio.csproj";
    private const string ExtensionSourcePath = "src/Vector.VisualStudio/VectorExtension.cs";
    private const string DocumentTypeSourcePath = "src/Vector.VisualStudio/VectorDocumentType.cs";
    private const string AboutCommandSourcePath = "src/Vector.VisualStudio/AboutVectorExtensionCommand.cs";
    private const string StringResourcesPath = "src/Vector.VisualStudio/.vsextension/string-resources.json";

    [Fact]
    public void Solution_ContainsVisualStudioExtensionProject()
    {
        var root = FindRepositoryRoot();
        var solution = File.ReadAllText(Path.Combine(root, "Vector.sln"));

        Assert.Contains(@"src\Vector.VisualStudio\Vector.VisualStudio.csproj", solution, StringComparison.Ordinal);
        Assert.Contains("{A1C79C49-CC56-4A32-A8A5-D67F41E12175}.Debug|Any CPU.Deploy.0 = Debug|Any CPU", solution, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtensionProject_TargetsNet8WindowsAndPinnedExtensibilitySdk()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, ExtensionProjectPath));

        Assert.Contains("<TargetFramework>net8.0-windows8.0</TargetFramework>", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.VisualStudio.Extensibility.Sdk\" Version=\"17.14.40608\"", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.VisualStudio.Extensibility.Build\" Version=\"17.14.40608\"", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Vector.Core", project, StringComparison.Ordinal);
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
    public void VectorDocumentType_RegistersVecAsText()
    {
        var root = FindRepositoryRoot();
        var documentTypeSource = File.ReadAllText(Path.Combine(root, DocumentTypeSourcePath));

        Assert.Contains("VectorFileExtension = \".vec\"", documentTypeSource, StringComparison.Ordinal);
        Assert.Contains("DocumentType.KnownValues.Text", documentTypeSource, StringComparison.Ordinal);
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
