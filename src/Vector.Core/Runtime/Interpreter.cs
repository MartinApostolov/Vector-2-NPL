using Vector.Core.Diagnostics;
using Vector.Core.Lexing;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Vector.Core.Syntax;
using Vector.Core.Syntax.Expressions;

namespace Vector.Core.Runtime;

/// <summary>
/// Evaluates Vector expressions against a lexical environment.
/// Statement execution is added in later runtime commits.
/// </summary>
public sealed class Interpreter
{
    public Interpreter(Environment? environment = null)
    {
        _environment = environment ?? new Environment();
    }

    private Environment _environment;

    public Environment CurrentEnvironment => _environment;

    public VectorValue Evaluate(ExpressionSyntax expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            LiteralExpression literal => EvaluateLiteral(literal),
            NameExpression name => _environment.Get(name.Name, name.Span),
            GroupingExpression grouping => Evaluate(grouping.Expression),
            UnaryExpression unary => EvaluateUnary(unary),
            BinaryExpression binary => EvaluateBinary(binary),
            AssignmentExpression assignment => EvaluateAssignment(assignment),
            _ => throw new InvalidOperationException(
                $"Expression type '{expression.GetType().Name}' is not implemented by this runtime stage.")
        };
    }

    private static VectorValue EvaluateLiteral(LiteralExpression expression) =>
        expression.Value switch
        {
            null => NothingValue.Instance,
            double number => new NumberValue(number),
            string text => new TextValue(text),
            bool boolean => new BooleanValue(boolean),
            _ => throw new InvalidOperationException(
                $"Unsupported literal payload type '{expression.Value.GetType().Name}'.")
        };

    private VectorValue EvaluateAssignment(AssignmentExpression expression)
    {
        // The right side is evaluated before the binding changes, as required by the
        // language specification. Indexed assignment is added with list runtime support.
        var value = Evaluate(expression.Value);

        if (expression.Target is NameExpression name)
        {
            _environment.Assign(name.Name, value, name.Span);
            return value;
        }

        throw new InvalidOperationException(
            "Indexed assignment is not implemented until list runtime support is added.");
    }

    private VectorValue EvaluateUnary(UnaryExpression expression)
    {
        var operand = Evaluate(expression.Operand);

        return expression.OperatorToken.Kind switch
        {
            TokenKind.Minus => new NumberValue(-RequireNumber(
                operand,
                expression.Operand.Span,
                "Unary '-' requires a number").Value),
            TokenKind.NotKeyword => new BooleanValue(!RequireBoolean(
                operand,
                expression.Operand.Span,
                "'not' requires a boolean").Value),
            _ => throw new InvalidOperationException(
                $"Unexpected unary operator '{expression.OperatorToken.Kind}'.")
        };
    }

    private VectorValue EvaluateBinary(BinaryExpression expression)
    {
        // Logical operators are handled separately so their right operand can be skipped.
        if (expression.OperatorToken.Kind == TokenKind.AndKeyword)
        {
            return EvaluateAnd(expression);
        }

        if (expression.OperatorToken.Kind == TokenKind.OrKeyword)
        {
            return EvaluateOr(expression);
        }

        // All other binary operands are deliberately evaluated left-to-right.
        var left = Evaluate(expression.Left);
        var right = Evaluate(expression.Right);

        return expression.OperatorToken.Kind switch
        {
            TokenKind.Plus => EvaluateAddition(left, right, expression),
            TokenKind.Minus => EvaluateNumericBinary(left, right, expression, (a, b) => a - b),
            TokenKind.Star => EvaluateNumericBinary(left, right, expression, (a, b) => a * b),
            TokenKind.Slash => EvaluateDivision(left, right, expression),
            TokenKind.Percent => EvaluateRemainder(left, right, expression),
            TokenKind.Less => EvaluateComparison(left, right, expression, (a, b) => a < b),
            TokenKind.LessOrEqual => EvaluateComparison(left, right, expression, (a, b) => a <= b),
            TokenKind.Greater => EvaluateComparison(left, right, expression, (a, b) => a > b),
            TokenKind.GreaterOrEqual => EvaluateComparison(left, right, expression, (a, b) => a >= b),
            TokenKind.EqualEqual => new BooleanValue(left == right),
            TokenKind.BangEqual => new BooleanValue(left != right),
            _ => throw new InvalidOperationException(
                $"Unexpected binary operator '{expression.OperatorToken.Kind}'.")
        };
    }

    private VectorValue EvaluateAnd(BinaryExpression expression)
    {
        var left = RequireBoolean(
            Evaluate(expression.Left),
            expression.Left.Span,
            "'and' requires boolean operands");

        if (!left.Value)
        {
            return new BooleanValue(false);
        }

        var right = RequireBoolean(
            Evaluate(expression.Right),
            expression.Right.Span,
            "'and' requires boolean operands");

        return new BooleanValue(right.Value);
    }

    private VectorValue EvaluateOr(BinaryExpression expression)
    {
        var left = RequireBoolean(
            Evaluate(expression.Left),
            expression.Left.Span,
            "'or' requires boolean operands");

        if (left.Value)
        {
            return new BooleanValue(true);
        }

        var right = RequireBoolean(
            Evaluate(expression.Right),
            expression.Right.Span,
            "'or' requires boolean operands");

        return new BooleanValue(right.Value);
    }

    private static VectorValue EvaluateAddition(
        VectorValue left,
        VectorValue right,
        BinaryExpression expression)
    {
        if (left is NumberValue leftNumber && right is NumberValue rightNumber)
        {
            return new NumberValue(leftNumber.Value + rightNumber.Value);
        }

        if (left is TextValue leftText && right is TextValue rightText)
        {
            return new TextValue(leftText.Value + rightText.Value);
        }

        throw CreateTypeError(
            $"Operator '+' requires two numbers or two text values, but received {left.TypeName} and {right.TypeName}.",
            expression.Span);
    }

    private static VectorValue EvaluateNumericBinary(
        VectorValue left,
        VectorValue right,
        BinaryExpression expression,
        Func<double, double, double> operation)
    {
        var leftNumber = RequireNumber(
            left,
            expression.Left.Span,
            $"Operator '{expression.OperatorToken.Text}' requires number operands");
        var rightNumber = RequireNumber(
            right,
            expression.Right.Span,
            $"Operator '{expression.OperatorToken.Text}' requires number operands");

        return new NumberValue(operation(leftNumber.Value, rightNumber.Value));
    }

    private static VectorValue EvaluateDivision(
        VectorValue left,
        VectorValue right,
        BinaryExpression expression)
    {
        var leftNumber = RequireNumber(
            left,
            expression.Left.Span,
            "Operator '/' requires number operands");
        var rightNumber = RequireNumber(
            right,
            expression.Right.Span,
            "Operator '/' requires number operands");

        if (rightNumber.Value == 0d)
        {
            throw new RuntimeError(
                DiagnosticCode.DivisionByZero,
                "Division by zero is not allowed.",
                expression.Right.Span);
        }

        return new NumberValue(leftNumber.Value / rightNumber.Value);
    }

    private static VectorValue EvaluateRemainder(
        VectorValue left,
        VectorValue right,
        BinaryExpression expression)
    {
        var leftNumber = RequireNumber(
            left,
            expression.Left.Span,
            "Operator '%' requires number operands");
        var rightNumber = RequireNumber(
            right,
            expression.Right.Span,
            "Operator '%' requires number operands");

        if (rightNumber.Value == 0d)
        {
            throw new RuntimeError(
                DiagnosticCode.DivisionByZero,
                "Remainder by zero is not allowed.",
                expression.Right.Span);
        }

        return new NumberValue(leftNumber.Value % rightNumber.Value);
    }

    private static VectorValue EvaluateComparison(
        VectorValue left,
        VectorValue right,
        BinaryExpression expression,
        Func<double, double, bool> comparison)
    {
        var leftNumber = RequireNumber(
            left,
            expression.Left.Span,
            $"Operator '{expression.OperatorToken.Text}' requires number operands");
        var rightNumber = RequireNumber(
            right,
            expression.Right.Span,
            $"Operator '{expression.OperatorToken.Text}' requires number operands");

        return new BooleanValue(comparison(leftNumber.Value, rightNumber.Value));
    }

    private static NumberValue RequireNumber(VectorValue value, SourceSpan span, string operation)
    {
        if (value is NumberValue number)
        {
            return number;
        }

        throw CreateTypeError($"{operation}, but received {value.TypeName}.", span);
    }

    private static BooleanValue RequireBoolean(VectorValue value, SourceSpan span, string operation)
    {
        if (value is BooleanValue boolean)
        {
            return boolean;
        }

        throw CreateTypeError($"{operation}, but received {value.TypeName}.", span);
    }

    private static RuntimeError CreateTypeError(string message, SourceSpan span) =>
        new(DiagnosticCode.RuntimeTypeError, message, span);
}
