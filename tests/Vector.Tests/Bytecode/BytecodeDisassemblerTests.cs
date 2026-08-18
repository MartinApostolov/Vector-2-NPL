using Vector.Core.Bytecode;
using Vector.Core.Modules;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Bytecode;

public sealed class BytecodeDisassemblerTests
{
    [Fact]
    public void DisassemblerProducesStableResolvedOutputIncludingNestedFunctions()
    {
        const string source = "let value = 42;\nimport lib.math;\nfunction add(a, b) { return a; }\n";
        var sourceText = new SourceText(source);

        var functionBuilder = new BytecodeBuilder();
        var aName = functionBuilder.AddName("a");
        functionBuilder.Emit(OpCode.GetVariable, aName, sourceText.GetSpan(61, 62));
        functionBuilder.Emit(OpCode.Return, sourceText.GetSpan(54, 63));
        var functionChunk = functionBuilder.Build("main.vec", source);
        var function = new BytecodeFunctionPrototype(
            "add",
            new[] { "a", "b" },
            functionChunk,
            sourceText.GetSpan(33, 65));

        var builder = new BytecodeBuilder();
        var constant = builder.AddConstant(new NumberValue(42));
        var valueName = builder.AddName("value");
        var module = builder.AddModule(new ModuleId(new[] { "lib", "math" }));
        var functionIndex = builder.AddFunction(function);

        builder.Emit(OpCode.Constant, constant, sourceText.GetSpan(12, 14));
        builder.Emit(OpCode.DeclareVariable, valueName, sourceText.GetSpan(0, 15));
        builder.Emit(OpCode.Import, module, sourceText.GetSpan(16, 32));
        builder.Emit(OpCode.MakeClosure, functionIndex, sourceText.GetSpan(33, 65));
        var jump = builder.EmitJump(OpCode.JumpIfFalse, sourceText.GetSpan(33, 41));
        builder.Emit(OpCode.BuildList, 2, sourceText.GetSpan(61, 62));
        builder.Emit(OpCode.Halt, sourceText.GetSpan(source.Length, source.Length));
        builder.PatchJump(jump, 6);

        var program = new BytecodeProgram(builder.Build("main.vec", source));

        var first = BytecodeDisassembler.Disassemble(program);
        var second = BytecodeDisassembler.Disassemble(program);

        Assert.Equal(first, second);
        Assert.Equal(
            "== <script> ==\n" +
            "source: main.vec\n" +
            "0000 Constant            0     ; constant[0] = 42 @ 1:13-1:15\n" +
            "0001 DeclareVariable     0     ; name[0] = value @ 1:1-1:16\n" +
            "0002 Import              0     ; module[0] = lib.math @ 2:1-2:17\n" +
            "0003 MakeClosure         0     ; function[0] = add/2 @ 3:1-3:33\n" +
            "0004 JumpIfFalse         6     ; -> 0006 @ 3:1-3:9\n" +
            "0005 BuildList           2     ; count = 2 @ 3:29-3:30\n" +
            "0006 Halt                       @ 4:1-4:1\n" +
            "-- function[0] add(a, b) @ 3:1-3:33 --\n" +
            "  == add ==\n" +
            "  source: main.vec\n" +
            "  0000 GetVariable         0     ; name[0] = a @ 3:29-3:30\n" +
            "  0001 Return                     @ 3:22-3:31\n",
            first);
    }

    [Fact]
    public void DisassemblerQuotesAndEscapesTextConstantsDeterministically()
    {
        var builder = new BytecodeBuilder();
        var constant = builder.AddConstant(new TextValue("quote: \" slash: \\ line\n"));
        builder.Emit(OpCode.Constant, constant, Span(0, 1));

        var text = BytecodeDisassembler.Disassemble(builder.Build(), "text-test");

        Assert.Contains("constant[0] = \"quote: \\\" slash: \\\\ line\\n\"", text);
    }

    [Fact]
    public void DisassemblerRejectsInvalidPoolOperandInsteadOfGuessing()
    {
        var chunk = new BytecodeChunk(
            new[] { new BytecodeInstruction(OpCode.Constant, 4, Span(0, 1)) },
            Array.Empty<VectorValue>(),
            Array.Empty<string>(),
            Array.Empty<ModuleId>(),
            Array.Empty<BytecodeFunctionPrototype>());

        var error = Assert.Throws<InvalidOperationException>(() =>
            BytecodeDisassembler.Disassemble(chunk));

        Assert.Contains("constant pool index 4", error.Message);
    }

    [Fact]
    public void DisassemblerUsesUnknownSourceAndCustomChunkLabelWhenNeeded()
    {
        var builder = new BytecodeBuilder();
        builder.Emit(OpCode.Nothing, Span(0, 1));

        var text = BytecodeDisassembler.Disassemble(builder.Build(), "fragment");

        Assert.StartsWith("== fragment ==\nsource: <unknown>\n", text);
    }

    private static SourceSpan Span(int startOffset, int endOffset) =>
        new(
            new SourcePosition(startOffset, 1, startOffset + 1),
            new SourcePosition(endOffset, 1, endOffset + 1));
}
