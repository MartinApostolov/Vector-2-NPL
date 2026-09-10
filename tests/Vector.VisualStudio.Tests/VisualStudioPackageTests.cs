namespace Vector.VisualStudio.Tests;

using System.IO.Compression;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Vector.VisualStudio.Diagnostics;
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
        Assert.Contains("Vector.LanguageConfiguration.pkgdef", entries);
        Assert.Contains("OutOfProc/Vector.VisualStudio.dll", entries);
        Assert.Contains("OutOfProc/LanguageServer/Vector.LanguageServer.exe", entries);
        Assert.Contains("OutOfProc/LanguageServer/Vector.Analysis.dll", entries);
        Assert.Contains("OutOfProc/ExecutionHost/Vector.ExecutionHost.exe", entries);
        Assert.Contains("OutOfProc/ExecutionHost/Vector.ExecutionProtocol.dll", entries);
        Assert.Contains("OutOfProc/ExecutionHost/Vector.Core.dll", entries);
        Assert.Contains("OutOfProc/ExecutionHost/Vector.Plugins.dll", entries);
        Assert.DoesNotContain("Vector.VisualStudio.dll", entries);
        Assert.Contains(".vsextension/settingsRegistration.json", entries);

        using JsonDocument extension = ReadJson(archive, ".vsextension/extension.json");
        JsonElement[] services = extension.RootElement.GetProperty("services").EnumerateArray().ToArray();
        Assert.NotEmpty(services);
        Assert.All(services, service =>
        {
            Assert.Equal(@".\OutOfProc", service.GetProperty("serviceBaseDirectory").GetString());
            Assert.Equal("Vector.VisualStudio.dll", service.GetProperty("entryPoint").GetProperty("assemblyPath").GetString());
            Assert.False(service.GetProperty("allowHostingInProcess").GetBoolean());
        });

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

        ZipArchiveEntry manifestEntry = Assert.Single(archive.Entries, candidate =>
            string.Equals(candidate.FullName.Replace('\\', '/'), "extension.vsixmanifest", StringComparison.Ordinal));
        using (Stream manifestStream = manifestEntry.Open())
        {
            var manifest = System.Xml.Linq.XDocument.Load(manifestStream);
            System.Xml.Linq.XNamespace schema = "http://schemas.microsoft.com/developer/vsx-schema/2011";
            System.Xml.Linq.XElement installation = Assert.Single(manifest.Descendants(schema + "Installation"));
            Assert.Equal("VSSDK+VisualStudio.Extensibility", installation.Attribute("ExtensionType")?.Value);
        }

        ZipArchiveEntry pkgdefEntry = Assert.Single(archive.Entries, candidate =>
            string.Equals(candidate.FullName.Replace('\\', '/'), "Vector.LanguageConfiguration.pkgdef", StringComparison.Ordinal));
        using var pkgdefReader = new StreamReader(pkgdefEntry.Open());
        string pkgdef = pkgdefReader.ReadToEnd();
        Assert.Contains("TextMate\\Repositories", pkgdef, StringComparison.Ordinal);
        Assert.Contains("TextMate\\LanguageConfiguration\\GrammarMapping", pkgdef, StringComparison.Ordinal);
        Assert.Contains("TextMate\\LanguageConfiguration\\ContentTypeMapping", pkgdef, StringComparison.Ordinal);
        Assert.Contains("\"source.vector\"", pkgdef, StringComparison.Ordinal);
        Assert.Contains("\"vector\"", pkgdef, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vsix_PackagedLanguageServerCompletesInitializeLifecycle()
    {
        string extractionRoot = Path.Combine(Path.GetTempPath(), $"vector-vsix-lsp-{Guid.NewGuid():N}");
        string serverDirectory = Path.Combine(extractionRoot, "OutOfProc", "LanguageServer");
        Directory.CreateDirectory(serverDirectory);

        try
        {
            using (ZipArchive archive = ZipFile.OpenRead(FindVsix()))
            {
                const string prefix = "OutOfProc/LanguageServer/";
                foreach (ZipArchiveEntry entry in archive.Entries.Where(candidate =>
                    candidate.FullName.Replace('\\', '/').StartsWith(prefix, StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(candidate.Name)))
                {
                    entry.ExtractToFile(Path.Combine(serverDirectory, entry.Name));
                }
            }

            string executable = Path.Combine(serverDirectory, "Vector.LanguageServer.exe");
            Assert.True(File.Exists(executable), $"Packaged language server was not extracted from '{FindVsix()}'.");
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(executable)
                {
                    WorkingDirectory = serverDirectory,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.StartInfo.Environment["DOTNET_ROOT"] = Path.Combine(extractionRoot, "visual-studio-private-net10-runtime");
            string runtimeSelection = DotNetChildProcessEnvironment.UseMachineWideRuntime(process.StartInfo);

            Assert.True(process.Start());
            try
            {
                await WriteMessageAsync(
                    process.StandardInput.BaseStream,
                    """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"processId":null,"rootUri":null,"capabilities":{}}}""");
                using JsonDocument initialize = await ReadMessageAsync(process.StandardOutput.BaseStream)
                    .WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(1, initialize.RootElement.GetProperty("id").GetInt32());
                Assert.Equal(
                    "Vector Language Server",
                    initialize.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
                Assert.Contains("visual-studio-private-net10-runtime", runtimeSelection, StringComparison.Ordinal);

                await WriteMessageAsync(process.StandardInput.BaseStream, """{"jsonrpc":"2.0","id":2,"method":"shutdown"}""");
                using JsonDocument shutdown = await ReadMessageAsync(process.StandardOutput.BaseStream)
                    .WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(2, shutdown.RootElement.GetProperty("id").GetInt32());
                await WriteMessageAsync(process.StandardInput.BaseStream, """{"jsonrpc":"2.0","method":"exit"}""");
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(0, process.ExitCode);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
        finally
        {
            Directory.Delete(extractionRoot, recursive: true);
        }
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
                    "Vector.VisualStudio.Package",
                    "bin",
                    "Release",
                    "net472",
                    "Vector.VisualStudio.Package.vsix");
                Assert.True(File.Exists(result), $"Expected Release VSIX at '{result}'.");
                return result;
            }
        }

        throw new InvalidOperationException("Could not locate Vector.sln from the test output directory.");
    }

    private static async Task WriteMessageAsync(Stream stream, string json)
    {
        byte[] body = Encoding.UTF8.GetBytes(json);
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        await stream.WriteAsync(header);
        await stream.WriteAsync(body);
        await stream.FlushAsync();
    }

    private static async Task<JsonDocument> ReadMessageAsync(Stream stream)
    {
        var headerBytes = new List<byte>();
        byte[] singleByte = new byte[1];
        while (!EndsWithHeaderTerminator(headerBytes))
        {
            int read = await stream.ReadAsync(singleByte);
            if (read == 0)
            {
                throw new EndOfStreamException("Packaged language server exited before replying to initialize.");
            }

            headerBytes.Add(singleByte[0]);
        }

        string header = Encoding.ASCII.GetString(headerBytes.ToArray());
        string lengthHeader = header.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        int contentLength = int.Parse(
            lengthHeader[(lengthHeader.IndexOf(':') + 1)..].Trim(),
            NumberStyles.None,
            CultureInfo.InvariantCulture);
        byte[] body = new byte[contentLength];
        await stream.ReadExactlyAsync(body);
        return JsonDocument.Parse(body);
    }

    private static bool EndsWithHeaderTerminator(List<byte> bytes)
    {
        int count = bytes.Count;
        return count >= 4
            && bytes[count - 4] == (byte)'\r'
            && bytes[count - 3] == (byte)'\n'
            && bytes[count - 2] == (byte)'\r'
            && bytes[count - 1] == (byte)'\n';
    }
}
