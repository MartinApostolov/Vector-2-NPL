namespace Vector.VisualStudio.Tests;

using System.IO.Compression;
using System.Text.Json;
using Xunit;

public sealed class VisualStudioPackageTests
{
    [Fact]
    public void Vsix_ContainsEditorMetadataLanguageServerExecutionHostAndSettings()
    {
        string vsixPath = FindVsix();
        using ZipArchive archive = ZipFile.OpenRead(vsixPath);
        string[] entries = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();

        Assert.Contains("Grammars/vector.tmLanguage.json", entries);
        Assert.Contains("LanguageConfiguration/vector-language-configuration.json", entries);
        Assert.Contains("LanguageServer/Vector.LanguageServer.exe", entries);
        Assert.Contains("LanguageServer/Vector.Analysis.dll", entries);
        Assert.Contains("ExecutionHost/Vector.ExecutionHost.exe", entries);
        Assert.Contains("ExecutionHost/Vector.ExecutionProtocol.dll", entries);
        Assert.Contains("ExecutionHost/Vector.Core.dll", entries);
        Assert.Contains("ExecutionHost/Vector.Plugins.dll", entries);
        Assert.Contains(".vsextension/settingsRegistration.json", entries);

        using JsonDocument extension = ReadJson(archive, ".vsextension/extension.json");
        JsonElement commands = extension.RootElement.GetProperty("commandSets")[0].GetProperty("commands");
        Assert.Contains(commands.EnumerateArray(), command =>
            command.GetProperty("name").GetString()?.EndsWith("RunVectorCommand", StringComparison.Ordinal) == true);
        Assert.Contains(commands.EnumerateArray(), command =>
            command.GetProperty("name").GetString()?.EndsWith("DisassembleVectorCommand", StringComparison.Ordinal) == true);

        using JsonDocument settings = ReadJson(archive, ".vsextension/settingsRegistration.json");
        JsonElement properties = settings.RootElement.GetProperty("properties");
        Assert.Equal("interpreter", properties.GetProperty("vector.defaultExecutionEngine").GetProperty("default").GetString());
        Assert.True(properties.GetProperty("vector.liveDiagnostics").GetProperty("default").GetBoolean());
        Assert.True(properties.TryGetProperty("vector.programRoot", out _));
        Assert.True(properties.TryGetProperty("vector.pluginPaths", out _));
    }

    private static JsonDocument ReadJson(ZipArchive archive, string path)
    {
        ZipArchiveEntry entry = Assert.Single(archive.Entries, candidate =>
            string.Equals(candidate.FullName.Replace('\\', '/'), path, StringComparison.Ordinal));
        using Stream stream = entry.Open();
        return JsonDocument.Parse(stream);
    }

    private static string FindVsix()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vector.sln")))
            {
                string result = Path.Combine(
                    directory.FullName,
                    "src",
                    "Vector.VisualStudio",
                    "bin",
                    "Release",
                    "net8.0-windows8.0",
                    "Vector.VisualStudio.vsix");
                Assert.True(File.Exists(result), $"Expected Release VSIX at '{result}'.");
                return result;
            }
        }

        throw new InvalidOperationException("Could not locate Vector.sln from the test output directory.");
    }
}
