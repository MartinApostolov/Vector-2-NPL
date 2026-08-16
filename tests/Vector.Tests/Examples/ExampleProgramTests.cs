using Vector.Core;
using Xunit;

namespace Vector.Tests.Examples;

public sealed class ExampleProgramTests
{
    public static IEnumerable<object[]> Programs =>
        new[]
        {
            Case("examples/01_hello.vec", "Hello, Vector!"),
            Case("examples/02_variables.vec", "20", "25"),
            Case("examples/03_conditions.vec", "B"),
            Case("examples/04_while_loop.vec", "1", "2", "3"),
            Case("examples/05_for_loop.vec", "1", "2", "3", "4"),
            Case("examples/06_functions.vec", "120"),
            Case("examples/07_lists.vec", "3", "Bob"),
            Case("examples/08_vectors.vec", "[5, 7, 9]", "[2, 4, 6]"),
            Case("examples/09_scopes.vec", "20", "10", "11"),
            Case("examples/10_modules/main.vec", "[4, 6]", "[8, 12]")
        };

    [Theory]
    [MemberData(nameof(Programs))]
    public void ExampleExecutesSuccessfullyWithExpectedOutput(
        string relativePath,
        string[] expectedOutput)
    {
        var repositoryRoot = FindRepositoryRoot();
        var examplePath = Path.Combine(
            repositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        var source = File.ReadAllText(examplePath);
        var programRoot = Path.GetDirectoryName(examplePath)!;

        var result = new VectorEngine().Execute(source, programRoot);

        var diagnosticText = string.Join(
            System.Environment.NewLine,
            result.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}"));
        Assert.True(result.Success, diagnosticText);
        Assert.Equal(expectedOutput, result.Output);
    }

    private static object[] Case(string relativePath, params string[] expectedOutput) =>
        new object[] { relativePath, expectedOutput };

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vector.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Vector repository root containing Vector.sln.");
    }
}
