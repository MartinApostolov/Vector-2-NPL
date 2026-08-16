namespace Vector.Core.Runtime.Host;

/// <summary>
/// Small host adapter whose output destination is supplied by the embedding application.
/// </summary>
public sealed class VectorHost : IVectorHost
{
    private readonly Action<string> _writeLine;

    public VectorHost(Action<string>? writeLine = null)
    {
        _writeLine = writeLine ?? (_ => { });
    }

    public void WriteLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _writeLine(text);
    }
}
