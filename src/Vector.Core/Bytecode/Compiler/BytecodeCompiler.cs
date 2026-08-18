using Vector.Core.Bytecode;
using Vector.Core.Lexing;
using Vector.Core.Runtime.Values;
using Vector.Core.Syntax;
using Vector.Core.Syntax.Expressions;
using Vector.Core.Syntax.Statements;

namespace Vector.Core.Bytecode.Compiler;

/// <summary>
/// Compiles supported Vector syntax into the in-memory bytecode model.
/// </summary>
internal sealed class BytecodeCompiler
{
    private const int IndexListRequirement = 0;
    private const int IndexedAssignmentListRequirement = 1;
    private const int IfBooleanRequirement = 0;
    private const int AndBooleanRequirement = 1;
    private const int OrBooleanRequirement = 2;

    public BytecodeCompilationResult Compile(
        CompilationUnit compilationUnit,
        string? sourceName = null,
        string? sourceText = null)
    {
        ArgumentNullException.ThrowIfNull(compilationUnit);

        var builder = new BytecodeBuilder();

        if (compilationUnit.Statements.Count == 0)
        {
            builder.Emit(OpCode.Nothing, compilationUnit.Span);
        }
        else
        {
            for (var index = 0; index < compilationUnit.Statements.Count; index++)
            {
                var statement = compilationUnit.Statements[index];
                CompileStatement(statement, builder);

                if (index < compilationUnit.Statements.Count - 1)
                {
                    builder.Emit(OpCode.Pop, statement.Span);
                }
            }
        }

        builder.Emit(OpCode.Halt, compilationUnit.Span);
        return new BytecodeCompilationResult(new BytecodeProgram(builder.Build(sourceName, sourceText)));
    }

    private static void CompileStatement(StatementSyntax statement, BytecodeBuilder builder)
    {
        switch (statement)
        {
            case ExpressionStatement expressionStatement:
                CompileExpression(expressionStatement.Expression, builder);
                return;

            case VariableDeclaration declaration:
                CompileExpression(declaration.Initializer, builder);
                builder.Emit(
                    OpCode.DeclareVariable,
                    builder.AddName(declaration.Name),
                    declaration.Span);
                return;

            case BlockStatement block:
                CompileBlock(block, builder);
                return;

            case IfStatement conditional:
                CompileIf(conditional, builder);
                return;

            default:
                throw new NotSupportedException(
                    $"Statement type '{statement.GetType().Name}' is not supported by the current bytecode compiler stage.");
        }
    }

    private static void CompileBlock(BlockStatement block, BytecodeBuilder builder)
    {
        builder.Emit(OpCode.EnterScope, block.Span);

        foreach (var statement in block.Statements)
        {
            CompileStatement(statement, builder);
            builder.Emit(OpCode.Pop, statement.Span);
        }

        builder.Emit(OpCode.ExitScope, block.Span);
        builder.Emit(OpCode.Nothing, block.Span);
    }

    private static void CompileIf(IfStatement conditional, BytecodeBuilder builder)
    {
        CompileExpression(conditional.Condition, builder);
        builder.Emit(OpCode.RequireBoolean, IfBooleanRequirement, conditional.Condition.Span);

        var falseJump = builder.EmitJump(OpCode.JumpIfFalse, conditional.Condition.Span);

        // The conditional jump inspects but does not consume its value. Remove the
        // true condition before executing the selected branch.
        builder.Emit(OpCode.Pop, conditional.Condition.Span);
        CompileStatement(conditional.ThenBranch, builder);
        var endJump = builder.EmitJump(OpCode.Jump, conditional.Span);

        builder.PatchJumpToCurrent(falseJump);

        // False conditions arrive here still on the operand stack.
        builder.Emit(OpCode.Pop, conditional.Condition.Span);
        if (conditional.ElseBranch is not null)
        {
            CompileStatement(conditional.ElseBranch, builder);
        }
        else
        {
            builder.Emit(OpCode.Nothing, conditional.Span);
        }

        builder.PatchJumpToCurrent(endJump);
    }

    private static void CompileExpression(ExpressionSyntax expression, BytecodeBuilder builder)
    {
        switch (expression)
        {
            case LiteralExpression literal:
                CompileLiteral(literal, builder);
                return;

            case ListExpression list:
                CompileList(list, builder);
                return;

            case IndexExpression index:
                CompileIndex(index, builder);
                return;

            case NameExpression name:
                builder.Emit(OpCode.GetVariable, builder.AddName(name.Name), name.Span);
                return;

            case GroupingExpression grouping:
                CompileExpression(grouping.Expression, builder);
                return;

            case UnaryExpression unary:
                CompileExpression(unary.Operand, builder);
                builder.Emit(MapUnaryOperator(unary.OperatorToken.Kind), unary.Span);
                return;

            case BinaryExpression binary:
                CompileBinary(binary, builder);
                return;

            case AssignmentExpression assignment:
                CompileAssignment(assignment, builder);
                return;

            default:
                throw new NotSupportedException(
                    $"Expression type '{expression.GetType().Name}' is not supported by the current bytecode compiler stage.");
        }
    }

    private static void CompileLiteral(LiteralExpression literal, BytecodeBuilder builder)
    {
        if (literal.Value is null)
        {
            builder.Emit(OpCode.Nothing, literal.Span);
            return;
        }

        VectorValue value = literal.Value switch
        {
            double number => new NumberValue(number),
            string text => new TextValue(text),
            bool boolean => new BooleanValue(boolean),
            _ => throw new InvalidOperationException(
                $"Unsupported literal payload type '{literal.Value.GetType().Name}'.")
        };

        var constantIndex = builder.AddConstant(value);
        builder.Emit(OpCode.Constant, constantIndex, literal.Span);
    }

    private static void CompileList(ListExpression list, BytecodeBuilder builder)
    {
        foreach (var element in list.Elements)
        {
            CompileExpression(element, builder);
        }

        builder.Emit(OpCode.BuildList, list.Elements.Count, list.Span);
    }

    private static void CompileIndex(IndexExpression index, BytecodeBuilder builder)
    {
        // Match the interpreter: evaluate and validate the target before evaluating
        // the index expression so an invalid target prevents index side effects.
        CompileExpression(index.Target, builder);
        builder.Emit(OpCode.RequireList, IndexListRequirement, index.Target.Span);
        CompileExpression(index.Index, builder);
        builder.Emit(OpCode.GetIndex, index.Span);
    }

    private static void CompileAssignment(AssignmentExpression assignment, BytecodeBuilder builder)
    {
        // Assignment is right-associative. Preserve the interpreter rule that the
        // right-hand side is evaluated before either assignment target is changed.
        CompileExpression(assignment.Value, builder);

        if (assignment.Target is NameExpression name)
        {
            var nameIndex = builder.AddName(name.Name);
            builder.Emit(OpCode.AssignVariable, nameIndex, name.Span);

            // Re-read the assigned binding so the expression result carries the full
            // assignment span while undefined-target diagnostics still point at the target name.
            builder.Emit(OpCode.GetVariable, nameIndex, assignment.Span);
            return;
        }

        if (assignment.Target is IndexExpression index)
        {
            // The target expression follows the RHS, but it must be validated before
            // the index expression runs, matching interpreter failure/side-effect order.
            CompileExpression(index.Target, builder);
            builder.Emit(
                OpCode.RequireList,
                IndexedAssignmentListRequirement,
                index.Target.Span);
            CompileExpression(index.Index, builder);
            builder.Emit(OpCode.SetIndex, assignment.Span);
            return;
        }

        throw new InvalidOperationException(
            $"Assignment target type '{assignment.Target.GetType().Name}' is not supported.");
    }

    private static void CompileBinary(BinaryExpression binary, BytecodeBuilder builder)
    {
        if (binary.OperatorToken.Kind == TokenKind.AndKeyword)
        {
            CompileShortCircuitLogical(
                binary,
                builder,
                OpCode.JumpIfFalse,
                AndBooleanRequirement);
            return;
        }

        if (binary.OperatorToken.Kind == TokenKind.OrKeyword)
        {
            CompileShortCircuitLogical(
                binary,
                builder,
                OpCode.JumpIfTrue,
                OrBooleanRequirement);
            return;
        }

        // Preserve the interpreter's left-to-right evaluation order.
        CompileExpression(binary.Left, builder);
        CompileExpression(binary.Right, builder);
        builder.Emit(MapBinaryOperator(binary.OperatorToken.Kind), binary.Span);
    }

    private static void CompileShortCircuitLogical(
        BinaryExpression binary,
        BytecodeBuilder builder,
        OpCode shortCircuitJump,
        int booleanRequirement)
    {
        CompileExpression(binary.Left, builder);
        builder.Emit(OpCode.RequireBoolean, booleanRequirement, binary.Left.Span);

        var endJump = builder.EmitJump(shortCircuitJump, binary.Left.Span);

        // When the left operand does not short-circuit, discard it and leave only
        // the validated right operand as the expression result.
        builder.Emit(OpCode.Pop, binary.Left.Span);
        CompileExpression(binary.Right, builder);
        builder.Emit(OpCode.RequireBoolean, booleanRequirement, binary.Right.Span);

        builder.PatchJumpToCurrent(endJump);
    }

    private static OpCode MapUnaryOperator(TokenKind kind) =>
        kind switch
        {
            TokenKind.Minus => OpCode.Negate,
            TokenKind.NotKeyword => OpCode.Not,
            _ => throw new InvalidOperationException($"Unexpected unary operator '{kind}'.")
        };

    private static OpCode MapBinaryOperator(TokenKind kind) =>
        kind switch
        {
            TokenKind.Plus => OpCode.Add,
            TokenKind.Minus => OpCode.Subtract,
            TokenKind.Star => OpCode.Multiply,
            TokenKind.Slash => OpCode.Divide,
            TokenKind.Percent => OpCode.Remainder,
            TokenKind.EqualEqual => OpCode.Equal,
            TokenKind.BangEqual => OpCode.NotEqual,
            TokenKind.Less => OpCode.Less,
            TokenKind.LessOrEqual => OpCode.LessOrEqual,
            TokenKind.Greater => OpCode.Greater,
            TokenKind.GreaterOrEqual => OpCode.GreaterOrEqual,
            _ => throw new InvalidOperationException($"Unexpected binary operator '{kind}'.")
        };
}
