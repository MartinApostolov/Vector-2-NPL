namespace Vector.Core.Runtime.Host;

/// <summary>
/// Optional host capability for Vector programs that need line-oriented input.
/// Output-only IVectorHost implementations do not need to implement this interface.
/// </summary>
public interface IVectorInputHost : IVectorHost
{
    string? ReadLine();
}
