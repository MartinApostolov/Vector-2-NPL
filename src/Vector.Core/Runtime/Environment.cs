using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;

namespace Vector.Core.Runtime;

/// <summary>
/// Stores Vector variables for one lexical scope and links to its enclosing scope.
/// </summary>
public sealed class Environment
{
    private readonly Dictionary<string, VectorValue> _values = new(StringComparer.Ordinal);

    public Environment(Environment? enclosing = null)
    {
        Enclosing = enclosing;
    }

    public Environment? Enclosing { get; }

    public void Declare(string name, VectorValue value, SourceSpan span)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(value);

        if (_values.ContainsKey(name))
        {
            throw new RuntimeError(
                DiagnosticCode.VariableAlreadyDeclared,
                $"Variable '{name}' is already declared in this scope.",
                span);
        }

        _values.Add(name, value);
    }

    public VectorValue Get(string name, SourceSpan span)
    {
        ValidateName(name);

        if (_values.TryGetValue(name, out var value))
        {
            return value;
        }

        if (Enclosing is not null)
        {
            return Enclosing.Get(name, span);
        }

        throw CreateUndefinedVariableError(name, span);
    }

    public void Assign(string name, VectorValue value, SourceSpan span)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(value);

        if (_values.ContainsKey(name))
        {
            _values[name] = value;
            return;
        }

        if (Enclosing is not null)
        {
            Enclosing.Assign(name, value, span);
            return;
        }

        throw CreateUndefinedVariableError(name, span);
    }

    private static RuntimeError CreateUndefinedVariableError(string name, SourceSpan span) =>
        new(
            DiagnosticCode.UndefinedVariable,
            $"Variable '{name}' is not declared.",
            span);

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A variable name cannot be empty.", nameof(name));
        }
    }
}
