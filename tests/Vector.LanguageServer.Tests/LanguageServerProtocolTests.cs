namespace Vector.LanguageServer.Tests;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Xunit;

public sealed class LanguageServerProtocolTests
{
    [Fact]
    public async Task Process_SupportsStandardLspLifecycleOverStdio()
    {
        string executable = Path.Combine(AppContext.BaseDirectory, "Vector.LanguageServer.exe");
        Assert.True(File.Exists(executable), $"Language server executable not found at '{executable}'.");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        Assert.True(process.Start());
        try
        {
            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"processId":null,"rootUri":null,"capabilities":{}}}""");

            using JsonDocument initializeResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(1, initializeResponse.RootElement.GetProperty("id").GetInt32());
            Assert.Equal(
                "Vector Language Server",
                initializeResponse.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());

            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","method":"initialized","params":{}}""");
            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///C:/workspace/protocol.vec","languageId":"vector","version":1,"text":"let value = ; range("}}}""");

            using JsonDocument diagnosticsNotification = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal("textDocument/publishDiagnostics", diagnosticsNotification.RootElement.GetProperty("method").GetString());
            Assert.NotEmpty(diagnosticsNotification.RootElement.GetProperty("params").GetProperty("diagnostics").EnumerateArray());

            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","id":2,"method":"textDocument/completion","params":{"textDocument":{"uri":"file:///C:/workspace/protocol.vec"},"position":{"line":0,"character":3}}}""");

            using JsonDocument completionResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(2, completionResponse.RootElement.GetProperty("id").GetInt32());
            Assert.Contains(
                completionResponse.RootElement.GetProperty("result").EnumerateArray(),
                item => item.GetProperty("label").GetString() == "let");

            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","id":3,"method":"textDocument/definition","params":{"textDocument":{"uri":"file:///C:/workspace/protocol.vec"},"position":{"line":0,"character":5}}}""");

            using JsonDocument definitionResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(3, definitionResponse.RootElement.GetProperty("id").GetInt32());
            Assert.Equal(
                "file:///C:/workspace/protocol.vec",
                definitionResponse.RootElement.GetProperty("result").GetProperty("uri").GetString());
            Assert.Equal(
                4,
                definitionResponse.RootElement.GetProperty("result").GetProperty("range").GetProperty("start").GetProperty("character").GetInt32());

            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","id":4,"method":"textDocument/signatureHelp","params":{"textDocument":{"uri":"file:///C:/workspace/protocol.vec"},"position":{"line":0,"character":20}}}""");

            using JsonDocument signatureResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(4, signatureResponse.RootElement.GetProperty("id").GetInt32());
            Assert.Equal(
                "range(start, end)",
                signatureResponse.RootElement.GetProperty("result").GetProperty("signatures")[0].GetProperty("label").GetString());

            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","id":6,"method":"textDocument/hover","params":{"textDocument":{"uri":"file:///C:/workspace/protocol.vec"},"position":{"line":0,"character":16}}}""");

            using JsonDocument hoverResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(6, hoverResponse.RootElement.GetProperty("id").GetInt32());
            Assert.Contains(
                "range(start, end)",
                hoverResponse.RootElement.GetProperty("result").GetProperty("contents").GetProperty("value").GetString(),
                StringComparison.Ordinal);

            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","id":7,"method":"textDocument/documentSymbol","params":{"textDocument":{"uri":"file:///C:/workspace/protocol.vec"}}}""");

            using JsonDocument symbolsResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(7, symbolsResponse.RootElement.GetProperty("id").GetInt32());
            Assert.Contains(
                symbolsResponse.RootElement.GetProperty("result").EnumerateArray(),
                item => item.GetProperty("name").GetString() == "value");

            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","id":8,"method":"textDocument/references","params":{"textDocument":{"uri":"file:///C:/workspace/protocol.vec"},"position":{"line":0,"character":5},"context":{"includeDeclaration":true}}}""");

            using JsonDocument referencesResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(8, referencesResponse.RootElement.GetProperty("id").GetInt32());
            Assert.Single(referencesResponse.RootElement.GetProperty("result").EnumerateArray());

            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","id":9,"method":"textDocument/rename","params":{"textDocument":{"uri":"file:///C:/workspace/protocol.vec"},"position":{"line":0,"character":5},"newName":"result"}}""");

            using JsonDocument renameResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(9, renameResponse.RootElement.GetProperty("id").GetInt32());
            JsonElement changes = renameResponse.RootElement.GetProperty("result").GetProperty("changes");
            Assert.Single(changes.GetProperty("file:///C:/workspace/protocol.vec").EnumerateArray());

            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","id":5,"method":"shutdown"}""");

            using JsonDocument shutdownResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(5, shutdownResponse.RootElement.GetProperty("id").GetInt32());
            Assert.Equal(JsonValueKind.Null, shutdownResponse.RootElement.GetProperty("result").ValueKind);

            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","method":"exit"}""");

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeout.Token);
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

    [Fact]
    public async Task Process_RespectsLiveDiagnosticsEnvironmentSetting()
    {
        string executable = Path.Combine(AppContext.BaseDirectory, "Vector.LanguageServer.exe");
        Assert.True(File.Exists(executable), $"Language server executable not found at '{executable}'.");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.Environment[VectorLanguageServerOptions.LiveDiagnosticsEnvironmentVariable] = "false";

        Assert.True(process.Start());
        try
        {
            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"processId":null,"rootUri":null,"capabilities":{}}}""");
            using JsonDocument initializeResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(1, initializeResponse.RootElement.GetProperty("id").GetInt32());
            await WriteMessageAsync(process.StandardInput.BaseStream, """{"jsonrpc":"2.0","method":"initialized","params":{}}""");
            await WriteMessageAsync(
                process.StandardInput.BaseStream,
                """{"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///C:/workspace/disabled.vec","languageId":"vector","version":1,"text":"let value = ;"}}}""");

            using JsonDocument notification = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal("textDocument/publishDiagnostics", notification.RootElement.GetProperty("method").GetString());
            Assert.Empty(notification.RootElement.GetProperty("params").GetProperty("diagnostics").EnumerateArray());

            await WriteMessageAsync(process.StandardInput.BaseStream, """{"jsonrpc":"2.0","id":2,"method":"shutdown"}""");
            using JsonDocument shutdownResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(2, shutdownResponse.RootElement.GetProperty("id").GetInt32());
            await WriteMessageAsync(process.StandardInput.BaseStream, """{"jsonrpc":"2.0","method":"exit"}""");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeout.Token);
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
                throw new EndOfStreamException("Language server closed stdout before returning an LSP response.");
            }

            headerBytes.Add(singleByte[0]);
        }

        string header = Encoding.ASCII.GetString(headerBytes.ToArray());
        string contentLengthHeader = header.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        int contentLength = int.Parse(
            contentLengthHeader[(contentLengthHeader.IndexOf(':') + 1)..].Trim(),
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
