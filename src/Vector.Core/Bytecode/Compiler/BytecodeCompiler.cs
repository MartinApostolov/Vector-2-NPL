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
                if (statement is not ExpressionStatement expressionStatement)
                {
                    throw new NotSupportedException(
                        $"Statement type '{statement.GetType().Name}' is not supported by the current bytecode compiler stage.");
                }

                CompileExpression(expressionStatement.Expression, builder);

                if (index < compilationUnit.Statements.Count - 1)
                {
                    builder.Emit(OpCode.Pop, expressionStatement.Span);
                }
            }
        }

        builder.Emit(OpCode.Halt, compilationUnit.Span);
        return new BytecodeCompilationResult(new BytecodeProgram(builder.Build(sourceName, sourceText)));
    }

    private static void CompileExpression(ExpressionSyntax expression, BytecodeBuilder builder)
    {
        switch (expression)
        {
            case LiteralExpression literal:
                CompileLiteral(literal, builder);
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

    private static void CompileBinary(BinaryExpression binary, BytecodeBuilder builder)
    {
        if (binary.OperatorToken.Kind is TokenKind.AndKeyword or TokenKind.OrKeyword)
        {
            throw new NotSupportedException(
                "Short-circuit logical expressions require jump bytecode and are implemented in a later compiler stage.");
        }

        // Preserve the interpreter's left-to-right evaluation order.
        CompileExpression(binary.Left, builder);
        CompileExpression(binary.Right, builder);
        builder.Emit(MapBinaryOperator(binary.OperatorToken.Kind), binary.Span);
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
