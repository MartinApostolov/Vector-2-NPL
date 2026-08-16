namespace Vector.Core.Runtime.Host;

/// <summary>
/// Host services used by Vector.Core without depending on a specific UI or console.
/// </summary>
public interface IVectorHost
{
    void WriteLine(string text);
}
