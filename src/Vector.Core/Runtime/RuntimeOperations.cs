using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;

namespace Vector.Core.Runtime;

/// <summary>
/// Backend-independent runtime operations shared by Vector execution engines.
/// </summary>
internal static class RuntimeOperations
{
    public static NumberValue Negate(VectorValue value, SourceSpan span) =>
        new(-RequireNumber(value, span, "Unary '-' requires a number").Value);

    public static BooleanValue LogicalNot(VectorValue value, SourceSpan span) =>
        new(!RequireBoolean(value, span, "'not' requires a boolean").Value);

    public static BooleanValue Equal(VectorValue left, VectorValue right) =>
        new(left == right);

    public static BooleanValue NotEqual(VectorValue left, VectorValue right) =>
        new(left != right);

    public static VectorValue Add(
        VectorValue left,
        VectorValue right,
        SourceSpan leftSpan,
        SourceSpan rightSpan,
        SourceSpan operationSpan)
    {
        if (left is NumberValue leftNumber && right is NumberValue rightNumber)
        {
            return new NumberValue(leftNumber.Value + rightNumber.Value);
        }

        if (left is TextValue leftText && right is TextValue rightText)
        {
            return new TextValue(leftText.Value + rightText.Value);
        }

        if (left is ListValue leftList && right is ListValue rightList)
        {
            return EvaluateVectorPair(
                leftList,
                rightList,
                leftSpan,
                rightSpan,
                operationSpan,
                "+",
                (a, b) => a + b);
        }

        throw CreateTypeError(
            $"Operator '+' requires two numbers, two text values, or two numeric lists, but received {left.TypeName} and {right.TypeName}.",
            operationSpan);
    }

    public static VectorValue Subtract(
        VectorValue left,
        VectorValue right,
        SourceSpan leftSpan,
        SourceSpan rightSpan,
        SourceSpan operationSpan)
    {
        if (left is NumberValue leftNumber && right is NumberValue rightNumber)
        {
            return new NumberValue(leftNumber.Value - rightNumber.Value);
        }

        if (left is ListValue leftList && right is ListValue rightList)
        {
            return EvaluateVectorPair(
                leftList,
                rightList,
                leftSpan,
                rightSpan,
                operationSpan,
                "-",
                (a, b) => a - b);
        }

        throw CreateTypeError(
            $"Operator '-' requires two numbers or two numeric lists, but received {left.TypeName} and {right.TypeName}.",
            operationSpan);
    }

    public static VectorValue Multiply(
        VectorValue left,
        VectorValue right,
        SourceSpan leftSpan,
        SourceSpan rightSpan,
        SourceSpan operationSpan)
    {
        if (left is NumberValue leftNumber && right is NumberValue rightNumber)
        {
            return new NumberValue(leftNumber.Value * rightNumber.Value);
        }

        if (left is ListValue leftList && right is NumberValue rightScalar)
        {
            return EvaluateScalarMultiplication(leftList, rightScalar, leftSpan);
        }

        if (left is NumberValue leftScalar && right is ListValue rightList)
        {
            return EvaluateScalarMultiplication(rightList, leftScalar, rightSpan);
        }

        throw CreateTypeError(
            $"Operator '*' requires two numbers or a numeric list and a number, but received {left.TypeName} and {right.TypeName}.",
            operationSpan);
    }

    public static VectorValue Divide(
        VectorValue left,
        VectorValue right,
        SourceSpan leftSpan,
        SourceSpan rightSpan)
    {
        var leftNumber = RequireNumber(left, leftSpan, "Operator '/' requires number operands");
        var rightNumber = RequireNumber(right, rightSpan, "Operator '/' requires number operands");

        if (rightNumber.Value == 0d)
        {
            throw new RuntimeError(
                DiagnosticCode.DivisionByZero,
                "Division by zero is not allowed.",
                rightSpan);
        }

        return new NumberValue(leftNumber.Value / rightNumber.Value);
    }

    public static VectorValue Remainder(
        VectorValue left,
        VectorValue right,
        SourceSpan leftSpan,
        SourceSpan rightSpan)
    {
        var leftNumber = RequireNumber(left, leftSpan, "Operator '%' requires number operands");
        var rightNumber = RequireNumber(right, rightSpan, "Operator '%' requires number operands");

        if (rightNumber.Value == 0d)
        {
            throw new RuntimeError(
                DiagnosticCode.DivisionByZero,
                "Remainder by zero is not allowed.",
                rightSpan);
        }

        return new NumberValue(leftNumber.Value % rightNumber.Value);
    }

    public static BooleanValue Compare(
        VectorValue left,
        VectorValue right,
        SourceSpan leftSpan,
        SourceSpan rightSpan,
        string operatorText,
        Func<double, double, bool> comparison)
    {
        ArgumentNullException.ThrowIfNull(operatorText);
        ArgumentNullException.ThrowIfNull(comparison);

        var leftNumber = RequireNumber(
            left,
            leftSpan,
            $"Operator '{operatorText}' requires number operands");
        var rightNumber = RequireNumber(
            right,
            rightSpan,
            $"Operator '{operatorText}' requires number operands");

        return new BooleanValue(comparison(leftNumber.Value, rightNumber.Value));
    }

    public static NumberValue RequireNumber(VectorValue value, SourceSpan span, string operation)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(operation);

        if (value is NumberValue number)
        {
            return number;
        }

        throw CreateTypeError($"{operation}, but received {value.TypeName}.", span);
    }

    public static BooleanValue RequireBoolean(VectorValue value, SourceSpan span, string operation)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(operation);

        if (value is BooleanValue boolean)
        {
            return boolean;
        }

        throw CreateTypeError($"{operation}, but received {value.TypeName}.", span);
    }

    public static ListValue RequireList(VectorValue value, SourceSpan span, string operation)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(operation);

        if (value is ListValue list)
        {
            return list;
        }

        throw CreateTypeError($"{operation}, but received {value.TypeName}.", span);
    }

    public static void RequireNumericList(ListValue list, SourceSpan span, string operation)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(operation);

        if (!list.IsNumericList)
        {
            throw CreateTypeError($"{operation}, but the list contains a non-number value.", span);
        }
    }

    public static int RequireListIndex(VectorValue value, SourceSpan span)
    {
        var number = RequireNumber(value, span, "A list index must be a number");

        if (!double.IsFinite(number.Value)
            || number.Value < 0d
            || number.Value != Math.Truncate(number.Value)
            || number.Value > int.MaxValue)
        {
            throw new RuntimeError(
                DiagnosticCode.InvalidListIndex,
                $"A list index must be a non-negative whole number, but received {number.Value}.",
                span);
        }

        return (int)number.Value;
    }

    public static VectorValue GetIndex(
        ListValue list,
        VectorValue indexValue,
        SourceSpan indexSpan)
    {
        ArgumentNullException.ThrowIfNull(list);

        var index = RequireListIndex(indexValue, indexSpan);
        EnsureIndexInRange(list, index, indexSpan);
        return list[index];
    }

    public static VectorValue SetIndex(
        ListValue list,
        VectorValue indexValue,
        SourceSpan indexSpan,
        VectorValue value,
        SourceSpan assignmentSpan)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(value);

        var index = RequireListIndex(indexValue, indexSpan);
        EnsureIndexInRange(list, index, indexSpan);

        if (list.WouldCreateCycle(value))
        {
            throw new RuntimeError(
                DiagnosticCode.CyclicList,
                "A Vector list cannot directly or indirectly contain itself.",
                assignmentSpan);
        }

        list[index] = value;
        return value;
    }

    public static void EnsureIndexInRange(ListValue list, int index, SourceSpan span)
    {
        ArgumentNullException.ThrowIfNull(list);

        if (index >= list.Count)
        {
            throw new RuntimeError(
                DiagnosticCode.ListIndexOutOfRange,
                $"List index {index} is outside the valid range for a list of length {list.Count}.",
                span);
        }
    }

    public static RuntimeError CreateTypeError(string message, SourceSpan span)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new RuntimeError(DiagnosticCode.RuntimeTypeError, message, span);
    }

    private static ListValue EvaluateVectorPair(
        ListValue left,
        ListValue right,
        SourceSpan leftSpan,
        SourceSpan rightSpan,
        SourceSpan operationSpan,
        string operatorText,
        Func<double, double, double> operation)
    {
        RequireNumericList(left, leftSpan, $"Operator '{operatorText}' requires numeric lists");
        RequireNumericList(right, rightSpan, $"Operator '{operatorText}' requires numeric lists");

        if (left.Count != right.Count)
        {
            throw new RuntimeError(
                DiagnosticCode.VectorLengthMismatch,
                $"Vector operation '{operatorText}' requires lists of equal length, but received lengths {left.Count} and {right.Count}.",
                operationSpan);
        }

        var result = new VectorValue[left.Count];
        for (var i = 0; i < left.Count; i++)
        {
            var leftNumber = (NumberValue)left.Elements[i];
            var rightNumber = (NumberValue)right.Elements[i];
            result[i] = new NumberValue(operation(leftNumber.Value, rightNumber.Value));
        }

        return new ListValue(result);
    }

    private static ListValue EvaluateScalarMultiplication(
        ListValue list,
        NumberValue scalar,
        SourceSpan listSpan)
    {
        RequireNumericList(list, listSpan, "Scalar multiplication requires a numeric list");

        var result = new VectorValue[list.Count];
        for (var i = 0; i < list.Count; i++)
        {
            var number = (NumberValue)list.Elements[i];
            result[i] = new NumberValue(number.Value * scalar.Value);
        }

        return new ListValue(result);
    }
}
