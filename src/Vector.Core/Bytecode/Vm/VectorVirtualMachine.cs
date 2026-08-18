using Vector.Core.Bytecode;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;

namespace Vector.Core.Bytecode.Vm;

/// <summary>
/// Executes Vector bytecode using an operand stack and explicit instruction pointer.
/// </summary>
internal sealed class VectorVirtualMachine
{
    public VmExecutionResult Execute(BytecodeProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        var chunk = program.EntryPoint;
        var stack = new Stack<VmStackValue>();
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
