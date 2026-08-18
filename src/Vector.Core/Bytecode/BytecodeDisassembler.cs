using System.Globalization;
using System.Text;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;

namespace Vector.Core.Bytecode;

/// <summary>
/// Produces stable human-readable output for Vector bytecode debugging and tests.
/// </summary>
internal static class BytecodeDisassembler
{
    public static string Disassemble(BytecodeProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        var builder = new StringBuilder();
        WriteChunk(builder, program.EntryPoint, "<script>", string.Empty);
        return builder.ToString();
    }

    public static string Disassemble(BytecodeChunk chunk, string label = "<script>")
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("A disassembly label cannot be empty.", nameof(label));
        }

        var builder = new StringBuilder();
        WriteChunk(builder, chunk, label, string.Empty);
        return builder.ToString();
    }

    private static void WriteChunk(
        StringBuilder builder,
        BytecodeChunk chunk,
        string label,
        string indent)
    {
        builder.Append(indent).Append("== ").Append(label).Append(" ==\n");
        builder.Append(indent).Append("source: ").Append(chunk.SourceName ?? "<unknown>").Append('\n');

        for (var index = 0; index < chunk.Instructions.Count; index++)
        {
            var instruction = chunk.Instructions[index];
            builder.Append(indent)
                .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(instruction.OpCode.ToString().PadRight(20));

            if (instruction.Operand is int operand)
            {
                builder.Append(operand.ToString(CultureInfo.InvariantCulture).PadRight(6));
                AppendOperandDescription(builder, chunk, instruction.OpCode, operand);
            }
            else
            {
                builder.Append("      ");
            }

            builder.Append(" @ ")
                .Append(FormatSpan(instruction.Span))
                .Append('\n');
        }

        for (var functionIndex = 0; functionIndex < chunk.Functions.Count; functionIndex++)
        {
            var function = chunk.Functions[functionIndex];
            builder.Append(indent)
                .Append("-- function[")
                .Append(functionIndex.ToString(CultureInfo.InvariantCulture))
                .Append("] ")
                .Append(function.Name)
                .Append('(')
                .Append(string.Join(", ", function.Parameters))
                .Append(") @ ")
                .Append(FormatSpan(function.DeclarationSpan))
                .Append(" --\n");

            WriteChunk(builder, function.Chunk, function.Name, indent + "  ");
        }
    }

    private static void AppendOperandDescription(
        StringBuilder builder,
        BytecodeChunk chunk,
        OpCode opCode,
        int operand)
    {
        switch (opCode)
        {
            case OpCode.Constant:
                builder.Append("; constant[")
                    .Append(operand.ToString(CultureInfo.InvariantCulture))
                    .Append("] = ")
                    .Append(FormatConstant(GetPoolItem(chunk.Constants, operand, "constant")));
                break;

            case OpCode.DeclareVariable:
            case OpCode.GetVariable:
            case OpCode.AssignVariable:
            case OpCode.GetQualifiedMember:
                builder.Append("; name[")
                    .Append(operand.ToString(CultureInfo.InvariantCulture))
                    .Append("] = ")
                    .Append(GetPoolItem(chunk.Names, operand, "name"));
                break;

            case OpCode.Import:
                builder.Append("; module[")
                    .Append(operand.ToString(CultureInfo.InvariantCulture))
                    .Append("] = ")
                    .Append(GetPoolItem(chunk.Modules, operand, "module").QualifiedName);
                break;

            case OpCode.MakeClosure:
                var function = GetPoolItem(chunk.Functions, operand, "function");
                builder.Append("; function[")
                    .Append(operand.ToString(CultureInfo.InvariantCulture))
                    .Append("] = ")
                    .Append(function.Name)
                    .Append('/')
                    .Append(function.Arity.ToString(CultureInfo.InvariantCulture));
                break;

            case OpCode.Jump:
            case OpCode.JumpIfFalse:
            case OpCode.JumpIfTrue:
                builder.Append("; -> ")
                    .Append(operand.ToString("D4", CultureInfo.InvariantCulture));
                break;

            case OpCode.BuildList:
            case OpCode.Call:
                builder.Append("; count = ")
                    .Append(operand.ToString(CultureInfo.InvariantCulture));
                break;

            default:
                builder.Append("; operand = ")
                    .Append(operand.ToString(CultureInfo.InvariantCulture));
                break;
        }
    }

    private static T GetPoolItem<T>(IReadOnlyList<T> pool, int index, string poolName)
    {
        if (index < 0 || index >= pool.Count)
        {
            throw new InvalidOperationException(
                $"Bytecode disassembly encountered invalid {poolName} pool index {index}.");
        }

        return pool[index];
    }

    private static string FormatConstant(VectorValue value) =>
        value is TextValue text
            ? QuoteText(text.Value)
            : VectorValueFormatter.Format(value);

    private static string QuoteText(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');

        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => character.ToString()
            });
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static string FormatSpan(SourceSpan span) =>
        $"{span.Start.Line}:{span.Start.Column}-{span.End.Line}:{span.End.Column}";
}
