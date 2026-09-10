namespace Vector.VisualStudio.Diagnostics;

using System.Diagnostics;
using System.Globalization;
using System.Text;

internal sealed class VectorLanguageServerLog
{
    private static readonly object FileLock = new();
    private readonly TraceSource traceSource;

    public VectorLanguageServerLog(TraceSource traceSource)
        : this(
            traceSource,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Vector",
                "VisualStudio",
                "language-server.log"))
    {
    }

    internal VectorLanguageServerLog(TraceSource traceSource, string filePath)
    {
        this.traceSource = traceSource;
        this.FilePath = Path.GetFullPath(filePath);
    }

    public string FilePath { get; }

    public void Information(string message) => this.Write(TraceEventType.Information, message);

    public void Error(string message) => this.Write(TraceEventType.Error, message);

    private void Write(TraceEventType eventType, string message)
    {
        string entry = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.Now:O} [extension {Environment.ProcessId}] {eventType}: {message}");

        try
        {
            this.traceSource.TraceEvent(eventType, 0, message);
            this.traceSource.Flush();
        }
        catch (Exception)
        {
            // The durable file remains available if the VS-provided trace listener fails.
        }

        try
        {
            lock (FileLock)
            {
                string? directory = Path.GetDirectoryName(this.FilePath);
                if (directory is not null)
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(this.FilePath, entry + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Diagnostics must never take down the out-of-process extension service.
        }
    }
}
