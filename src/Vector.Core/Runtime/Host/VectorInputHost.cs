namespace Vector.Core.Runtime.Host;

/// <summary>
/// Small host adapter whose output and line-input delegates are supplied by the embedding application.
/// </summary>
public sealed class VectorInputHost : IVectorInputHost
{
    private readonly Action<string> _writeLine;
    private readonly Func<string?> _readLine;

    public VectorInputHost(Action<string>? writeLine, Func<string?> readLine)
    {
        _writeLine = writeLine ?? (_ => { });
        _readLine = readLine ?? throw new ArgumentNullException(nameof(readLine));
    }

    public void WriteLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _writeLine(text);
    }

    public string? ReadLine() => _readLine();
}
