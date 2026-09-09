namespace Vector.Tests.VisualStudio;

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

public sealed class VectorEditorConfigurationTests
{
    private static readonly string[] ReservedKeywords =
    [
        "let", "if", "else", "while", "for", "in", "function", "return", "break", "continue",
        "true", "false", "nothing", "and", "or", "not", "import",
    ];

    [Fact]
    public void TextMateGrammar_IsValidAndTargetsVecFiles()
    {
        using JsonDocument grammar = ReadJson("src/Vector.VisualStudio/Grammars/vector.tmLanguage.json");
        JsonElement root = grammar.RootElement;

        Assert.Equal("source.vector", root.GetProperty("scopeName").GetString());
        Assert.Contains(root.GetProperty("fileTypes").EnumerateArray(), value => value.GetString() == "vec");
        Assert.True(root.GetProperty("patterns").GetArrayLength() >= 10);
    }

    [Fact]
    public void TextMateGrammar_CoversEveryReservedKeyword()
    {
        using JsonDocument grammar = ReadJson("src/Vector.VisualStudio/Grammars/vector.tmLanguage.json");
        string[] matches = grammar.RootElement.GetProperty("patterns").EnumerateArray()
            .Where(pattern => pattern.TryGetProperty("match", out _))
            .Select(pattern => pattern.GetProperty("match").GetString()!)
            .ToArray();

        foreach (string keyword in ReservedKeywords)
        {
            Assert.Contains(matches, pattern => Regex.IsMatch(keyword, pattern));
        }
    }

    [Fact]
    public void LanguageConfiguration_DefinesVectorCommentsBracketsAndPairs()
    {
        using JsonDocument configuration = ReadJson(
            "src/Vector.VisualStudio/LanguageConfiguration/vector-language-configuration.json");
        JsonElement root = configuration.RootElement;

        Assert.Equal("//", root.GetProperty("comments").GetProperty("lineComment").GetString());
        Assert.Equal("/*", root.GetProperty("comments").GetProperty("blockComment")[0].GetString());
        Assert.Equal("*/", root.GetProperty("comments").GetProperty("blockComment")[1].GetString());
        Assert.Equal(3, root.GetProperty("brackets").GetArrayLength());
        Assert.Equal(4, root.GetProperty("autoClosingPairs").GetArrayLength());
        Assert.Equal(4, root.GetProperty("surroundingPairs").GetArrayLength());
    }

    [Fact]
    public void Pkgdef_MapsVectorScopeToPackagedConfiguration()
    {
        string root = FindRepositoryRoot();
        string pkgdef = File.ReadAllText(Path.Combine(root, "src/Vector.VisualStudio/Vector.LanguageConfiguration.pkgdef"));

        Assert.Contains("TextMate\\Repositories", pkgdef, StringComparison.Ordinal);
        Assert.Contains("$PackageFolder$\\Grammars", pkgdef, StringComparison.Ordinal);
        Assert.Contains("\"source.vector\"", pkgdef, StringComparison.Ordinal);
        Assert.Contains("vector-language-configuration.json", pkgdef, StringComparison.Ordinal);
    }

    [Fact]
    public void VsixManifest_RegistersLanguageConfigurationPkgdef()
    {
        string root = FindRepositoryRoot();
        XDocument manifest = XDocument.Load(Path.Combine(root, "src/Vector.VisualStudio/source.extension.vsixmanifest"));
        XNamespace schema = "http://schemas.microsoft.com/developer/vsx-schema/2011";

        XElement asset = Assert.Single(manifest.Descendants(schema + "Asset"));
        Assert.Equal("Microsoft.VisualStudio.VsPackage", asset.Attribute("Type")?.Value);
        Assert.Equal("Vector.LanguageConfiguration.pkgdef", asset.Attribute("Path")?.Value);
    }

    private static JsonDocument ReadJson(string relativePath)
    {
        string root = FindRepositoryRoot();
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(root, relativePath)));
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
