using Vector.Core.Bytecode;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Core.Bytecode.Vm;

/// <summary>
/// Executes Vector bytecode using an operand stack and explicit instruction pointer.
/// </summary>
internal sealed class VectorVirtualMachine
{
    private const int IndexListRequirement = 0;
    private const int IndexedAssignmentListRequirement = 1;
    private const int IfBooleanRequirement = 0;
    private const int AndBooleanRequirement = 1;
    private const int OrBooleanRequirement = 2;

    public VmExecutionResult Execute(BytecodeProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        var chunk = program.EntryPoint;
        var stack = new Stack<VmStackValue>();
        var environment = new RuntimeEnvironment();
        var instructionPointer = 0;

        try
        {
            while (instructionPointer < chunk.Instructions.Count)
            {
                var instruction = chunk.Instructions[instructionPointer];
                instructionPointer++;

                switch (instruction.OpCode)
                {
                    case OpCode.Constant:
                        ExecuteConstant(chunk, instruction, stack);
                        break;

                    case OpCode.Nothing:
                        stack.Push(new VmStackValue(NothingValue.Instance, instruction.Span));
                        break;

                    case OpCode.Pop:
                        Pop(stack, instruction);
                        break;

                    case OpCode.Negate:
                    {
                        var operand = Pop(stack, instruction);
                        stack.Push(new VmStackValue(
                            RuntimeOperations.Negate(operand.Value, operand.Span),
                            instruction.Span));
                        break;
                    }

                    case OpCode.Not:
                    {
                        var operand = Pop(stack, instruction);
                        stack.Push(new VmStackValue(
                            RuntimeOperations.LogicalNot(operand.Value, operand.Span),
                            instruction.Span));
                        break;
                    }

                    case OpCode.Add:
                        ExecuteBinary(stack, instruction, (left, right) =>
                            RuntimeOperations.Add(
                                left.Value,
                                right.Value,
                                left.Span,
                                right.Span,
                                instruction.Span));
                        break;

                    case OpCode.Subtract:
                        ExecuteBinary(stack, instruction, (left, right) =>
                            RuntimeOperations.Subtract(
                                left.Value,
                                right.Value,
                                left.Span,
                                right.Span,
                                instruction.Span));
                        break;

                    case OpCode.Multiply:
                        ExecuteBinary(stack, instruction, (left, right) =>
                            RuntimeOperations.Multiply(
                                left.Value,
                                right.Value,
                                left.Span,
                                right.Span,
                                instruction.Span));
                        break;

                    case OpCode.Divide:
                        ExecuteBinary(stack, instruction, (left, right) =>
                            RuntimeOperations.Divide(
                                left.Value,
                                right.Value,
                                left.Span,
                                right.Span));
                        break;

                    case OpCode.Remainder:
                        ExecuteBinary(stack, instruction, (left, right) =>
                            RuntimeOperations.Remainder(
                                left.Value,
                                right.Value,
                                left.Span,
                                right.Span));
                        break;

                    case OpCode.Equal:
                        ExecuteBinary(stack, instruction, (left, right) =>
                            RuntimeOperations.Equal(left.Value, right.Value));
                        break;

                    case OpCode.NotEqual:
                        ExecuteBinary(stack, instruction, (left, right) =>
                            RuntimeOperations.NotEqual(left.Value, right.Value));
                        break;

                    case OpCode.Less:
                        ExecuteComparison(stack, instruction, "<", (left, right) => left < right);
                        break;

                    case OpCode.LessOrEqual:
                        ExecuteComparison(stack, instruction, "<=", (left, right) => left <= right);
                        break;

                    case OpCode.Greater:
                        ExecuteComparison(stack, instruction, ">", (left, right) => left > right);
                        break;

                    case OpCode.GreaterOrEqual:
                        ExecuteComparison(stack, instruction, ">=", (left, right) => left >= right);
                        break;

                    case OpCode.EnterScope:
                        environment = new RuntimeEnvironment(environment);
                        break;

                    case OpCode.ExitScope:
                        environment = ExitScope(environment);
                        break;

                    case OpCode.DeclareVariable:
                        ExecuteDeclareVariable(chunk, instruction, stack, environment);
                        break;

                    case OpCode.GetVariable:
                        ExecuteGetVariable(chunk, instruction, stack, environment);
                        break;

                    case OpCode.AssignVariable:
                        ExecuteAssignVariable(chunk, instruction, stack, environment);
                        break;

                    case OpCode.BuildList:
                        ExecuteBuildList(instruction, stack);
                        break;

                    case OpCode.RequireList:
                        ExecuteRequireList(instruction, stack);
                        break;

                    case OpCode.RequireBoolean:
                        ExecuteRequireBoolean(instruction, stack);
                        break;

                    case OpCode.GetIndex:
                        ExecuteGetIndex(instruction, stack);
                        break;

                    case OpCode.SetIndex:
                        ExecuteSetIndex(instruction, stack);
                        break;

                    case OpCode.Jump:
                        instructionPointer = GetJumpTarget(chunk, instruction);
                        break;

                    case OpCode.JumpIfFalse:
                        if (PeekBoolean(stack, instruction).Value == false)
                        {
                            instructionPointer = GetJumpTarget(chunk, instruction);
                        }
                        break;

                    case OpCode.JumpIfTrue:
                        if (PeekBoolean(stack, instruction).Value)
                        {
                            instructionPointer = GetJumpTarget(chunk, instruction);
                        }
                        break;

                    case OpCode.Halt:
                        return new VmExecutionResult(FinalResult(stack));

                    default:
                        throw new InvalidOperationException(
                            $"Opcode '{instruction.OpCode}' is not implemented by the current VM stage.");
                }
            }
        }
        catch (RuntimeError error) when (chunk.SourceText is not null)
        {
            throw error.WithSource(chunk.SourceName, chunk.SourceText);
        }

        throw new InvalidOperationException("Bytecode execution reached the end of the chunk without Halt.");
    }

    private static void ExecuteConstant(
        BytecodeChunk chunk,
        BytecodeInstruction instruction,
        Stack<VmStackValue> stack)
    {
        var operand = RequireOperand(instruction);
        if (operand < 0 || operand >= chunk.Constants.Count)
        {
            throw new InvalidOperationException(
                $"Constant instruction references invalid constant pool index {operand}.");
        }

        stack.Push(new VmStackValue(chunk.Constants[operand], instruction.Span));
    }

    private static void ExecuteDeclareVariable(
        BytecodeChunk chunk,
        BytecodeInstruction instruction,
        Stack<VmStackValue> stack,
        RuntimeEnvironment environment)
    {
        var name = GetName(chunk, instruction);
        var initializer = Pop(stack, instruction);
        environment.Declare(name, initializer.Value, instruction.Span);
        stack.Push(new VmStackValue(NothingValue.Instance, instruction.Span));
    }

    private static void ExecuteGetVariable(
        BytecodeChunk chunk,
        BytecodeInstruction instruction,
        Stack<VmStackValue> stack,
        RuntimeEnvironment environment)
    {
        var name = GetName(chunk, instruction);
        stack.Push(new VmStackValue(environment.Get(name, instruction.Span), instruction.Span));
    }

    private static void ExecuteAssignVariable(
        BytecodeChunk chunk,
        BytecodeInstruction instruction,
        Stack<VmStackValue> stack,
        RuntimeEnvironment environment)
    {
        var name = GetName(chunk, instruction);
        var value = Pop(stack, instruction);
        environment.Assign(name, value.Value, instruction.Span);
    }

    private static void ExecuteBuildList(
        BytecodeInstruction instruction,
        Stack<VmStackValue> stack)
    {
        var count = RequireOperand(instruction);
        if (count < 0)
        {
            throw new InvalidOperationException("BuildList requires a non-negative element count.");
        }

        if (stack.Count < count)
        {
            throw new InvalidOperationException(
                $"Operand stack contains {stack.Count} values, but BuildList requires {count}.");
        }

        var elements = new VectorValue[count];
        for (var index = count - 1; index >= 0; index--)
        {
            elements[index] = Pop(stack, instruction).Value;
        }

        stack.Push(new VmStackValue(new ListValue(elements), instruction.Span));
    }

    private static void ExecuteRequireList(
        BytecodeInstruction instruction,
        Stack<VmStackValue> stack)
    {
        if (stack.Count == 0)
        {
            throw new InvalidOperationException(
                $"Operand stack underflow while executing '{instruction.OpCode}'.");
        }

        var target = stack.Peek();
        var operation = RequireOperand(instruction) switch
        {
            IndexListRequirement => "Indexing requires a list target",
            IndexedAssignmentListRequirement => "Indexed assignment requires a list target",
            var requirement => throw new InvalidOperationException(
                $"RequireList has unknown requirement kind {requirement}.")
        };

        RuntimeOperations.RequireList(target.Value, target.Span, operation);
    }

    private static void ExecuteRequireBoolean(
        BytecodeInstruction instruction,
        Stack<VmStackValue> stack)
    {
        if (stack.Count == 0)
        {
            throw new InvalidOperationException(
                $"Operand stack underflow while executing '{instruction.OpCode}'.");
        }

        var value = stack.Peek();
        var operation = RequireOperand(instruction) switch
        {
            IfBooleanRequirement => "An 'if' condition must be a boolean",
            AndBooleanRequirement => "'and' requires boolean operands",
            OrBooleanRequirement => "'or' requires boolean operands",
            var requirement => throw new InvalidOperationException(
                $"RequireBoolean has unknown requirement kind {requirement}.")
        };

        RuntimeOperations.RequireBoolean(value.Value, instruction.Span, operation);
    }

    private static void ExecuteGetIndex(
        BytecodeInstruction instruction,
        Stack<VmStackValue> stack)
    {
        var index = Pop(stack, instruction);
        var target = Pop(stack, instruction);
        var list = RuntimeOperations.RequireList(
            target.Value,
            target.Span,
            "Indexing requires a list target");

        var value = RuntimeOperations.GetIndex(list, index.Value, index.Span);
        stack.Push(new VmStackValue(value, instruction.Span));
    }

    private static void ExecuteSetIndex(
        BytecodeInstruction instruction,
        Stack<VmStackValue> stack)
    {
        var index = Pop(stack, instruction);
        var target = Pop(stack, instruction);
        var value = Pop(stack, instruction);
        var list = RuntimeOperations.RequireList(
            target.Value,
            target.Span,
            "Indexed assignment requires a list target");

        var result = RuntimeOperations.SetIndex(
            list,
            index.Value,
            index.Span,
            value.Value,
            instruction.Span);
        stack.Push(new VmStackValue(result, instruction.Span));
    }

    private static int GetJumpTarget(BytecodeChunk chunk, BytecodeInstruction instruction)
    {
        var target = RequireOperand(instruction);
        if (target < 0 || target > chunk.Instructions.Count)
        {
            throw new InvalidOperationException(
                $"{instruction.OpCode} instruction references invalid jump target {target}.");
        }

        return target;
    }

    private static BooleanValue PeekBoolean(
        Stack<VmStackValue> stack,
        BytecodeInstruction instruction)
    {
        if (stack.Count == 0)
        {
            throw new InvalidOperationException(
                $"Operand stack underflow while executing '{instruction.OpCode}'.");
        }

        return stack.Peek().Value as BooleanValue
            ?? throw new InvalidOperationException(
                $"Opcode '{instruction.OpCode}' requires a validated boolean value on the operand stack.");
    }

    private static RuntimeEnvironment ExitScope(RuntimeEnvironment environment)
    {
        if (environment.Enclosing is null)
        {
            throw new InvalidOperationException(
                "Bytecode attempted to exit the root lexical environment.");
        }

        return environment.Enclosing;
    }

    private static string GetName(BytecodeChunk chunk, BytecodeInstruction instruction)
    {
        var operand = RequireOperand(instruction);
        if (operand < 0 || operand >= chunk.Names.Count)
        {
            throw new InvalidOperationException(
                $"{instruction.OpCode} instruction references invalid name pool index {operand}.");
        }

        return chunk.Names[operand];
    }

    private static void ExecuteBinary(
        Stack<VmStackValue> stack,
        BytecodeInstruction instruction,
        Func<VmStackValue, VmStackValue, VectorValue> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var right = Pop(stack, instruction);
        var left = Pop(stack, instruction);
        var result = operation(left, right);
        stack.Push(new VmStackValue(result, instruction.Span));
    }

    private static void ExecuteComparison(
        Stack<VmStackValue> stack,
        BytecodeInstruction instruction,
        string operatorText,
        Func<double, double, bool> comparison)
    {
        ExecuteBinary(stack, instruction, (left, right) =>
            RuntimeOperations.Compare(
                left.Value,
                right.Value,
                left.Span,
                right.Span,
                operatorText,
                comparison));
    }

    private static VmStackValue Pop(
        Stack<VmStackValue> stack,
        BytecodeInstruction instruction)
    {
        if (stack.Count == 0)
        {
            throw new InvalidOperationException(
                $"Operand stack underflow while executing '{instruction.OpCode}'.");
        }

        return stack.Pop();
    }

    private static int RequireOperand(BytecodeInstruction instruction) =>
        instruction.Operand
        ?? throw new InvalidOperationException(
            $"Opcode '{instruction.OpCode}' requires an operand.");

    private static VectorValue FinalResult(Stack<VmStackValue> stack)
    {
        if (stack.Count == 0)
        {
            return NothingValue.Instance;
        }

        if (stack.Count != 1)
        {
            throw new InvalidOperationException(
                $"Bytecode halted with {stack.Count} values on the operand stack; expected exactly one.");
        }

        return stack.Peek().Value;
    }

    private readonly record struct VmStackValue(VectorValue Value, SourceSpan Span);
}
