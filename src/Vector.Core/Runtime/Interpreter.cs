using Vector.Core.Diagnostics;
using Vector.Core.Lexing;
using Vector.Core.Modules;
using Vector.Core.Runtime.Builtins;
using Vector.Core.Runtime.Callable;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.ControlFlow;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Vector.Core.Syntax;
using Vector.Core.Syntax.Expressions;
using Vector.Core.Syntax.Statements;

namespace Vector.Core.Runtime;

/// <summary>
/// Evaluates Vector expressions and executes statements against a lexical environment.
/// </summary>
public sealed class Interpreter
{
    public Interpreter(
        Environment? environment = null,
        IVectorHost? host = null,
        ModuleLoader? moduleLoader = null)
    {
        _environment = environment ?? new Environment();
        Host = host ?? new VectorHost();
        _moduleLoader = moduleLoader;
        _builtins = new Dictionary<string, VectorValue>(StringComparer.Ordinal)
        {
            ["print"] = new PrintBuiltin(Host),
            ["length"] = new LengthBuiltin(),
            ["concat"] = new ConcatBuiltin(),
            ["text"] = new TextBuiltin(),
            ["number"] = new NumberBuiltin(),
            ["type"] = new TypeBuiltin(),
            ["range"] = new RangeBuiltin()
        };
    }

    private readonly IReadOnlyDictionary<string, VectorValue> _builtins;
    private readonly ModuleLoader? _moduleLoader;
    private readonly HashSet<ModuleId> _importedModules = new();
    private Environment _environment;
    private string? _sourceName;
    private string? _sourceText;

    public Environment CurrentEnvironment => _environment;

    public IVectorHost Host { get; }

    public VectorValue Execute(CompilationUnit compilationUnit)
    {
        ArgumentNullException.ThrowIfNull(compilationUnit);
        return ExecuteCompilationUnit(compilationUnit);
    }

    public VectorValue Execute(CompilationUnit compilationUnit, string? sourceName, string sourceText)
    {
        ArgumentNullException.ThrowIfNull(compilationUnit);
        ArgumentNullException.ThrowIfNull(sourceText);

        var previousSourceName = _sourceName;
        var previousSourceText = _sourceText;
        _sourceName = sourceName;
        _sourceText = sourceText;

        try
        {
            return ExecuteCompilationUnit(compilationUnit);
        }
        catch (RuntimeError error)
        {
            throw error.WithSource(_sourceName, _sourceText);
        }
        finally
        {
            _sourceName = previousSourceName;
            _sourceText = previousSourceText;
        }
    }

    private VectorValue ExecuteCompilationUnit(CompilationUnit compilationUnit)
    {
        var result = (VectorValue)NothingValue.Instance;
        foreach (var statement in compilationUnit.Statements)
        {
            result = Execute(statement);
        }

        return result;
    }

    public VectorValue Execute(StatementSyntax statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        return statement switch
        {
            ExpressionStatement expressionStatement => Evaluate(expressionStatement.Expression),
            VariableDeclaration declaration => ExecuteVariableDeclaration(declaration),
            BlockStatement block => ExecuteBlock(block),
            IfStatement conditional => ExecuteIf(conditional),
            FunctionDeclaration function => ExecuteFunctionDeclaration(function),
            ReturnStatement returnStatement => ExecuteReturn(returnStatement),
            WhileStatement loop => ExecuteWhile(loop),
            ForStatement loop => ExecuteFor(loop),
            BreakStatement breakStatement => throw new BreakSignal(breakStatement.Span),
            ContinueStatement continueStatement => throw new ContinueSignal(continueStatement.Span),
            ImportStatement import => ExecuteImport(import),
            _ => throw new InvalidOperationException(
                $"Statement type '{statement.GetType().Name}' is not implemented by this runtime stage.")
        };
    }

    private VectorValue ExecuteImport(ImportStatement import)
    {
        if (_moduleLoader is null)
        {
            throw new InvalidOperationException(
                "Executing an import requires an interpreter configured with a ModuleLoader.");
        }

        var moduleId = ModuleId.FromImport(import);
        _moduleLoader.Import(moduleId, Host);
        _importedModules.Add(moduleId);
        return NothingValue.Instance;
    }

    private VectorValue ExecuteFunctionDeclaration(FunctionDeclaration declaration)
    {
        // Capture the environment object itself. The binding is installed immediately
        // afterward, which also makes the function's own name visible for recursion.
        var function = new UserFunction(
            declaration,
            _environment,
            _sourceName,
            _sourceText);
        _environment.Declare(declaration.Name, function, declaration.Span);
        return NothingValue.Instance;
    }

    private VectorValue ExecuteReturn(ReturnStatement statement)
    {
        var value = statement.Expression is null
            ? (VectorValue)NothingValue.Instance
            : Evaluate(statement.Expression);

        throw new ReturnSignal(value, statement.Span);
    }

    private VectorValue ExecuteVariableDeclaration(VariableDeclaration declaration)
    {
        // The initializer is evaluated before the new binding is introduced.
        var value = Evaluate(declaration.Initializer);
        _environment.Declare(declaration.Name, value, declaration.Span);
        return NothingValue.Instance;
    }

    private VectorValue ExecuteBlock(BlockStatement block)
    {
        var previous = _environment;
        _environment = new Environment(previous);

        try
        {
            foreach (var statement in block.Statements)
            {
                Execute(statement);
            }

            return NothingValue.Instance;
        }
        finally
        {
            _environment = previous;
        }
    }

    private VectorValue ExecuteIf(IfStatement conditional)
    {
        var condition = RequireBoolean(
            Evaluate(conditional.Condition),
            conditional.Condition.Span,
            "An 'if' condition must be a boolean");

        if (condition.Value)
        {
            ExecuteBlock(conditional.ThenBranch);
        }
        else if (conditional.ElseBranch is not null)
        {
            Execute(conditional.ElseBranch);
        }

        return NothingValue.Instance;
    }

    private VectorValue ExecuteWhile(WhileStatement loop)
    {
        while (true)
        {
            var condition = RequireBoolean(
                Evaluate(loop.Condition),
                loop.Condition.Span,
                "A 'while' condition must be a boolean");

            if (!condition.Value)
            {
                break;
            }

            try
            {
                ExecuteBlock(loop.Body);
            }
            catch (ContinueSignal)
            {
                continue;
            }
            catch (BreakSignal)
            {
                break;
            }
        }

        return NothingValue.Instance;
    }

    private VectorValue ExecuteFor(ForStatement loop)
    {
        // The iterable expression is evaluated exactly once. The element sequence is
        // then captured as a shallow snapshot so replacing entries in the original
        // list during the loop does not change which values this iteration visits.
        var iterable = RequireList(
            Evaluate(loop.Iterable),
            loop.Iterable.Span,
            "A 'for' loop requires a list iterable");
        var snapshot = iterable.Elements.ToArray();

        foreach (var item in snapshot)
        {
            try
            {
                ExecuteForIteration(loop, item);
            }
            catch (ContinueSignal)
            {
                continue;
            }
            catch (BreakSignal)
            {
                break;
            }
        }

        return NothingValue.Instance;
    }

    private void ExecuteForIteration(ForStatement loop, VectorValue item)
    {
        var previous = _environment;
        _environment = new Environment(previous);

        try
        {
            // The iteration environment is also the body block's lexical scope. This
            // keeps the loop variable local, gives every iteration a fresh scope, and
            // makes same-scope redeclaration rules apply normally inside the body.
            _environment.Declare(loop.VariableName, item, loop.Span);

            foreach (var statement in loop.Body.Statements)
            {
                Execute(statement);
            }
        }
        finally
        {
            _environment = previous;
        }
    }

    internal VectorValue InvokeUserFunction(UserFunction function, IReadOnlyList<VectorValue> arguments)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != function.Arity)
        {
            throw new ArgumentException(
                $"Function '{function.Name}' requires {function.Arity} arguments, but received {arguments.Count}.",
                nameof(arguments));
        }

        var previousEnvironment = _environment;
        var previousSourceName = _sourceName;
        var previousSourceText = _sourceText;
        _environment = new Environment(function.Closure);

        if (function.SourceText is not null)
        {
            _sourceName = function.SourceName;
            _sourceText = function.SourceText;
        }

        try
        {
            for (var i = 0; i < function.Declaration.Parameters.Count; i++)
            {
                _environment.Declare(
                    function.Declaration.Parameters[i],
                    arguments[i],
                    function.Declaration.Span);
            }

            try
            {
                // The function invocation environment is also the function body's lexical
                // scope, so parameters and top-level body declarations share one scope.
                foreach (var statement in function.Declaration.Body.Statements)
                {
                    Execute(statement);
                }
            }
            catch (ReturnSignal signal)
            {
                return signal.Value;
            }

            return NothingValue.Instance;
        }
        catch (RuntimeError error)
        {
            if (_sourceText is not null)
            {
                throw error.WithSource(_sourceName, _sourceText);
            }

            throw;
        }
        finally
        {
            _environment = previousEnvironment;
            _sourceName = previousSourceName;
            _sourceText = previousSourceText;
        }
    }

    public VectorValue Evaluate(ExpressionSyntax expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            LiteralExpression literal => EvaluateLiteral(literal),
            NameExpression name => EvaluateName(name),
            QualifiedNameExpression qualifiedName => EvaluateQualifiedName(qualifiedName),
            GroupingExpression grouping => Evaluate(grouping.Expression),
            UnaryExpression unary => EvaluateUnary(unary),
            BinaryExpression binary => EvaluateBinary(binary),
            AssignmentExpression assignment => EvaluateAssignment(assignment),
            ListExpression list => EvaluateList(list),
            IndexExpression index => EvaluateIndex(index),
            CallExpression call => EvaluateCall(call),
            _ => throw new InvalidOperationException(
                $"Expression type '{expression.GetType().Name}' is not implemented by this runtime stage.")
        };
    }

    private VectorValue EvaluateName(NameExpression expression)
    {
        try
        {
            return _environment.Get(expression.Name, expression.Span);
        }
        catch (RuntimeError error) when (error.Code == DiagnosticCode.UndefinedVariable
            && _builtins.TryGetValue(expression.Name, out var builtin))
        {
            return builtin;
        }
    }

    private VectorValue EvaluateQualifiedName(QualifiedNameExpression expression)
    {
        if (_moduleLoader is null)
        {
            throw new RuntimeError(
                DiagnosticCode.UndefinedVariable,
                $"Qualified module name '{expression.QualifiedName}' is not available.",
                expression.Span);
        }

        var accessibleModules = GetAccessibleModules();

        foreach (var moduleId in accessibleModules.OrderByDescending(id => id.Segments.Count))
        {
            if (expression.PathSegments.Count != moduleId.Segments.Count + 1
                || !PathStartsWith(expression.PathSegments, moduleId.Segments))
            {
                continue;
            }

            if (!_moduleLoader.TryGetLoaded(moduleId, out var module) || module is null)
            {
                continue;
            }

            var memberName = expression.PathSegments[^1];
            return module.Environment.Get(memberName, expression.Span);
        }

        throw new RuntimeError(
            DiagnosticCode.UndefinedVariable,
            $"Qualified module member '{expression.QualifiedName}' is not available in this scope.",
            expression.Span);
    }

    private IEnumerable<ModuleId> GetAccessibleModules()
    {
        if (_moduleLoader is not null
            && _moduleLoader.TryGetModuleForEnvironment(_environment, out var owner)
            && owner is not null)
        {
            return owner.Imports;
        }

        return _importedModules;
    }

    private static bool PathStartsWith(
        IReadOnlyList<string> path,
        IReadOnlyList<string> prefix)
    {
        if (path.Count < prefix.Count)
        {
            return false;
        }

        for (var i = 0; i < prefix.Count; i++)
        {
            if (!string.Equals(path[i], prefix[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private VectorValue EvaluateCall(CallExpression expression)
    {
        // Evaluate the callee first. Arity can then be validated before evaluating any
        // arguments, so an invalid call cannot partially perform argument side effects.
        var callee = Evaluate(expression.Callee);
        if (callee is not IVectorCallable callable)
        {
            throw CreateTypeError(
                $"Only functions can be called, but received {callee.TypeName}.",
                expression.Callee.Span);
        }

        if (expression.Arguments.Count != callable.Arity)
        {
            throw new RuntimeError(
                DiagnosticCode.ArgumentCountMismatch,
                $"Function expects {callable.Arity} arguments, but received {expression.Arguments.Count}.",
                expression.Span);
        }

        var arguments = new VectorValue[expression.Arguments.Count];
        for (var i = 0; i < expression.Arguments.Count; i++)
        {
            arguments[i] = Evaluate(expression.Arguments[i]);
        }

        try
        {
            return callable.Call(this, arguments);
        }
        catch (BuiltinRuntimeException error)
        {
            throw new RuntimeError(error.Code, error.Message, expression.Span);
        }
        catch (NativeRuntimeException error)
        {
            throw new RuntimeError(error.Code, error.Message, expression.Span);
        }
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

    private VectorValue EvaluateList(ListExpression expression)
    {
        var elements = new VectorValue[expression.Elements.Count];

        for (var i = 0; i < expression.Elements.Count; i++)
        {
            elements[i] = Evaluate(expression.Elements[i]);
        }

        return new ListValue(elements);
    }

    private VectorValue EvaluateIndex(IndexExpression expression)
    {
        // Target and index are ordinary expression operands and evaluate left-to-right.
        var target = RequireList(
            Evaluate(expression.Target),
            expression.Target.Span,
            "Indexing requires a list target");
        var index = RequireListIndex(Evaluate(expression.Index), expression.Index.Span);

        EnsureIndexInRange(target, index, expression.Index.Span);
        return target[index];
    }

    private VectorValue EvaluateAssignment(AssignmentExpression expression)
    {
        // Assignment is right-associative, so the value is evaluated before the target
        // binding or indexed element is changed.
        var value = Evaluate(expression.Value);

        if (expression.Target is NameExpression name)
        {
            _environment.Assign(name.Name, value, name.Span);
            return value;
        }

        if (expression.Target is IndexExpression indexExpression)
        {
            var target = RequireList(
                Evaluate(indexExpression.Target),
                indexExpression.Target.Span,
                "Indexed assignment requires a list target");
            var index = RequireListIndex(Evaluate(indexExpression.Index), indexExpression.Index.Span);

            EnsureIndexInRange(target, index, indexExpression.Index.Span);

            if (target.WouldCreateCycle(value))
            {
                throw new RuntimeError(
                    DiagnosticCode.CyclicList,
                    "A Vector list cannot directly or indirectly contain itself.",
                    expression.Span);
            }

            target[index] = value;
            return value;
        }

        throw new InvalidOperationException(
            $"Assignment target type '{expression.Target.GetType().Name}' is not supported.");
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
            TokenKind.Minus => EvaluateSubtraction(left, right, expression),
            TokenKind.Star => EvaluateMultiplication(left, right, expression),
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

        if (left is ListValue leftList && right is ListValue rightList)
        {
            return EvaluateVectorPair(
                leftList,
                rightList,
                expression,
                (a, b) => a + b);
        }

        throw CreateTypeError(
            $"Operator '+' requires two numbers, two text values, or two numeric lists, but received {left.TypeName} and {right.TypeName}.",
            expression.Span);
    }

    private static VectorValue EvaluateSubtraction(
        VectorValue left,
        VectorValue right,
        BinaryExpression expression)
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
                expression,
                (a, b) => a - b);
        }

        throw CreateTypeError(
            $"Operator '-' requires two numbers or two numeric lists, but received {left.TypeName} and {right.TypeName}.",
            expression.Span);
    }

    private static VectorValue EvaluateMultiplication(
        VectorValue left,
        VectorValue right,
        BinaryExpression expression)
    {
        if (left is NumberValue leftNumber && right is NumberValue rightNumber)
        {
            return new NumberValue(leftNumber.Value * rightNumber.Value);
        }

        if (left is ListValue leftList && right is NumberValue rightScalar)
        {
            return EvaluateScalarMultiplication(leftList, rightScalar, expression.Left.Span);
        }

        if (left is NumberValue leftScalar && right is ListValue rightList)
        {
            return EvaluateScalarMultiplication(rightList, leftScalar, expression.Right.Span);
        }

        throw CreateTypeError(
            $"Operator '*' requires two numbers or a numeric list and a number, but received {left.TypeName} and {right.TypeName}.",
            expression.Span);
    }

    private static ListValue EvaluateVectorPair(
        ListValue left,
        ListValue right,
        BinaryExpression expression,
        Func<double, double, double> operation)
    {
        RequireNumericList(
            left,
            expression.Left.Span,
            $"Operator '{expression.OperatorToken.Text}' requires numeric lists");
        RequireNumericList(
            right,
            expression.Right.Span,
            $"Operator '{expression.OperatorToken.Text}' requires numeric lists");

        if (left.Count != right.Count)
        {
            throw new RuntimeError(
                DiagnosticCode.VectorLengthMismatch,
                $"Vector operation '{expression.OperatorToken.Text}' requires lists of equal length, but received lengths {left.Count} and {right.Count}.",
                expression.Span);
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

    private static ListValue RequireList(VectorValue value, SourceSpan span, string operation)
    {
        if (value is ListValue list)
        {
            return list;
        }

        throw CreateTypeError($"{operation}, but received {value.TypeName}.", span);
    }

    private static void RequireNumericList(ListValue list, SourceSpan span, string operation)
    {
        if (!list.IsNumericList)
        {
            throw CreateTypeError($"{operation}, but the list contains a non-number value.", span);
        }
    }

    private static int RequireListIndex(VectorValue value, SourceSpan span)
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

    private static void EnsureIndexInRange(ListValue list, int index, SourceSpan span)
    {
        if (index >= list.Count)
        {
            throw new RuntimeError(
                DiagnosticCode.ListIndexOutOfRange,
                $"List index {index} is outside the valid range for a list of length {list.Count}.",
                span);
        }
    }

    private static RuntimeError CreateTypeError(string message, SourceSpan span) =>
        new(DiagnosticCode.RuntimeTypeError, message, span);
}
