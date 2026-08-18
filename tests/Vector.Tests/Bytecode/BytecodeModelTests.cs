using Vector.Core.Bytecode;
using Vector.Core.Modules;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Bytecode;

public sealed class BytecodeModelTests
{
    [Fact]
    public void OpCodeUsesByteStorageAndContainsPlannedInstructionFamilies()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(OpCode)));

        Assert.Contains(OpCode.Constant, Enum.GetValues<OpCode>());
        Assert.Contains(OpCode.DeclareVariable, Enum.GetValues<OpCode>());
        Assert.Contains(OpCode.BuildList, Enum.GetValues<OpCode>());
        Assert.Contains(OpCode.JumpIfFalse, Enum.GetValues<OpCode>());
        Assert.Contains(OpCode.MakeClosure, Enum.GetValues<OpCode>());
        Assert.Contains(OpCode.Import, Enum.GetValues<OpCode>());
        Assert.Contains(OpCode.Halt, Enum.GetValues<OpCode>());
    }

    [Fact]
    public void InstructionRetainsOpcodeOperandAndSourceSpan()
    {
        var span = Span(2, 5);
        var withoutOperand = new BytecodeInstruction(OpCode.Nothing, span);
        var withOperand = new BytecodeInstruction(OpCode.Constant, 3, span);

        Assert.Equal(OpCode.Nothing, withoutOperand.OpCode);
        Assert.False(withoutOperand.HasOperand);
        Assert.Null(withoutOperand.Operand);
        Assert.Equal(span, withoutOperand.Span);

        Assert.Equal(OpCode.Constant, withOperand.OpCode);
        Assert.True(withOperand.HasOperand);
        Assert.Equal(3, withOperand.Operand);
        Assert.Equal(span, withOperand.Span);
    }

    [Fact]
    public void BuilderAssignsStablePoolIndexes()
    {
        var builder = new BytecodeBuilder();
        var math = new ModuleId(new[] { "lib", "math" });

        Assert.Equal(0, builder.AddConstant(new NumberValue(10)));
        Assert.Equal(1, builder.AddConstant(new NumberValue(10)));

        Assert.Equal(0, builder.AddName("value"));
        Assert.Equal(0, builder.AddName("value"));
        Assert.Equal(1, builder.AddName("other"));

        Assert.Equal(0, builder.AddModule(math));
        Assert.Equal(0, builder.AddModule(new ModuleId(new[] { "lib", "math" })));

        builder.Emit(OpCode.Halt, Span(0, 0));
        var chunk = builder.Build("main.vec", "10;");

        Assert.Equal(2, chunk.Constants.Count);
        Assert.Equal(new NumberValue(10), chunk.Constants[0]);
        Assert.Equal(new NumberValue(10), chunk.Constants[1]);
        Assert.Equal(new[] { "value", "other" }, chunk.Names);
        Assert.Single(chunk.Modules);
        Assert.Equal(math, chunk.Modules[0]);
        Assert.Equal("main.vec", chunk.SourceName);
        Assert.Equal("10;", chunk.SourceText);
    }

    [Fact]
    public void BuiltChunkDoesNotChangeWhenBuilderContinues()
    {
        var builder = new BytecodeBuilder();
        builder.AddName("first");
        builder.Emit(OpCode.Nothing, Span(0, 1));

        var firstChunk = builder.Build();

        builder.AddName("second");
        builder.Emit(OpCode.Pop, Span(1, 2));
        var secondChunk = builder.Build();

        Assert.Single(firstChunk.Names);
        Assert.Single(firstChunk.Instructions);
        Assert.Equal(2, secondChunk.Names.Count);
        Assert.Equal(2, secondChunk.Instructions.Count);
    }

    [Fact]
    public void FunctionPrototypeRetainsParametersChunkAndDeclarationSpan()
    {
        var bodyBuilder = new BytecodeBuilder();
        bodyBuilder.Emit(OpCode.Return, Span(20, 26));
        var body = bodyBuilder.Build("main.vec", "function add(a, b) { return a; }");
        var declarationSpan = Span(0, 34);

        var function = new BytecodeFunctionPrototype(
            "add",
            new[] { "a", "b" },
            body,
            declarationSpan);

        var rootBuilder = new BytecodeBuilder();
        Assert.Equal(0, rootBuilder.AddFunction(function));
        rootBuilder.Emit(OpCode.MakeClosure, 0, declarationSpan);
        rootBuilder.Emit(OpCode.Halt, Span(34, 34));
        var root = rootBuilder.Build("main.vec", "function add(a, b) { return a; }");
        var program = new BytecodeProgram(root);

        Assert.Same(root, program.EntryPoint);
        Assert.Single(root.Functions);
        Assert.Equal("add", root.Functions[0].Name);
        Assert.Equal(new[] { "a", "b" }, root.Functions[0].Parameters);
        Assert.Equal(2, root.Functions[0].Arity);
        Assert.Same(body, root.Functions[0].Chunk);
        Assert.Equal(declarationSpan, root.Functions[0].DeclarationSpan);
    }

    [Fact]
    public void JumpPlaceholdersCanBePatchedWithoutLosingSourceSpan()
    {
        foreach (var jumpOpCode in new[] { OpCode.Jump, OpCode.JumpIfFalse, OpCode.JumpIfTrue })
        {
            var builder = new BytecodeBuilder();
            var jumpSpan = Span(2, 8);
            var jumpIndex = builder.EmitJump(jumpOpCode, jumpSpan);
            builder.Emit(OpCode.Nothing, Span(9, 16));
            builder.PatchJumpToCurrent(jumpIndex);
            builder.Emit(OpCode.Halt, Span(16, 16));

            var chunk = builder.Build();
            var jump = chunk.Instructions[jumpIndex];

            Assert.Equal(jumpOpCode, jump.OpCode);
            Assert.Equal(2, jump.Operand);
            Assert.Equal(jumpSpan, jump.Span);
        }
    }

    [Fact]
    public void BuilderRejectsInvalidOrIncompleteJumpPatching()
    {
        var builder = new BytecodeBuilder();
        var plainIndex = builder.Emit(OpCode.Nothing, Span(0, 1));
        var jumpIndex = builder.EmitJump(OpCode.Jump, Span(2, 3));

        Assert.Throws<ArgumentException>(() => builder.EmitJump(OpCode.Add, Span(4, 5)));
        Assert.Throws<InvalidOperationException>(() => builder.PatchJump(plainIndex, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.PatchJump(jumpIndex, 99));
        Assert.Throws<InvalidOperationException>(() => builder.Build());

        builder.PatchJump(jumpIndex, 0);
        Assert.Throws<InvalidOperationException>(() => builder.PatchJump(jumpIndex, 0));

        var chunk = builder.Build();
        Assert.Equal(0, chunk.Instructions[jumpIndex].Operand);
    }

    [Fact]
    public void BuilderValidatesPoolInputs()
    {
        var builder = new BytecodeBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddConstant(null!));
        Assert.Throws<ArgumentException>(() => builder.AddName(""));
        Assert.Throws<ArgumentException>(() => builder.AddName("   "));
        Assert.Throws<ArgumentNullException>(() => builder.AddModule(null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddFunction(null!));
    }

    private static SourceSpan Span(int startOffset, int endOffset) =>
        new(
            new SourcePosition(startOffset, 1, startOffset + 1),
            new SourcePosition(endOffset, 1, endOffset + 1));
}
