using Vector.Core.Diagnostics;

namespace Vector.Core.Runtime.Native;

/// <summary>
/// Represents a deliberate failure raised at the C#/.NET native-function boundary.
/// The interpreter translates this into a normal Vector runtime diagnostic at the
/// source call site.
/// </summary>
public sealed class NativeRuntimeException : Exception
{
    public NativeRuntimeException(DiagnosticCode code, string message)
        : this(code, message, null)
    {
    }

    public NativeRuntimeException(DiagnosticCode code, string message, Exception? innerException)
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A native runtime error message cannot be empty.", nameof(message));
        }

        Code = code;
    }

    public DiagnosticCode Code { get; }
}
