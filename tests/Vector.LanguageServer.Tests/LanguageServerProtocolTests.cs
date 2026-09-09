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
                """{"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///C:/workspace/protocol.vec","languageId":"vector","version":1,"text":"let value = ;"}}}""");

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
                """{"jsonrpc":"2.0","id":3,"method":"shutdown"}""");

            using JsonDocument shutdownResponse = await ReadMessageAsync(process.StandardOutput.BaseStream);
            Assert.Equal(3, shutdownResponse.RootElement.GetProperty("id").GetInt32());
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
