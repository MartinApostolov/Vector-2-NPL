using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;
using RuntimeError = Vector.Core.Runtime.RuntimeError;
using Xunit;

namespace Vector.Tests.Runtime;

public sealed class EnvironmentTests
{
    [Fact]
    public void DeclarationStoresValueInCurrentScope()
    {
        var environment = new RuntimeEnvironment();
        var value = new NumberValue(10);

        environment.Declare("score", value, Span(0, 5));

        Assert.Same(value, environment.Get("score", Span(0, 5)));
    }

    [Fact]
    public void AssignmentMayChangeRuntimeValueType()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("value", new TextValue("20"), Span(0, 5));

        environment.Assign("value", new NumberValue(20), Span(10, 15));

        Assert.Equal(new NumberValue(20), environment.Get("value", Span(10, 15)));
    }

    [Fact]
    public void LookupSearchesEnclosingScopes()
    {
        var global = new RuntimeEnvironment();
        var local = new RuntimeEnvironment(global);
        var value = new BooleanValue(true);
        global.Declare("enabled", value, Span(0, 7));

        Assert.Same(value, local.Get("enabled", Span(12, 19)));
    }

    [Fact]
    public void NestedScopeMayShadowOuterVariable()
    {
        var global = new RuntimeEnvironment();
        var local = new RuntimeEnvironment(global);
        global.Declare("x", new NumberValue(10), Span(0, 1));
        local.Declare("x", new NumberValue(20), Span(5, 6));

        Assert.Equal(new NumberValue(10), global.Get("x", Span(0, 1)));
        Assert.Equal(new NumberValue(20), local.Get("x", Span(5, 6)));
    }

    [Fact]
    public void SameScopeRedeclarationIsRuntimeError()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("x", new NumberValue(10), Span(0, 1));
        var secondDeclarationSpan = Span(8, 9);

        var error = Assert.Throws<RuntimeError>(() =>
            environment.Declare("x", new NumberValue(20), secondDeclarationSpan));

        Assert.Equal(DiagnosticCode.VariableAlreadyDeclared, error.Code);
        Assert.Equal(secondDeclarationSpan, error.Span);
        Assert.Contains("'x'", error.Message);
        Assert.Equal(new NumberValue(10), environment.Get("x", Span(0, 1)));
    }

    [Fact]
    public void AssignmentUpdatesNearestMatchingVariable()
    {
        var global = new RuntimeEnvironment();
        var middle = new RuntimeEnvironment(global);
        var local = new RuntimeEnvironment(middle);
        global.Declare("x", new NumberValue(1), Span(0, 1));
        middle.Declare("x", new NumberValue(2), Span(4, 5));

        local.Assign("x", new NumberValue(3), Span(8, 9));

        Assert.Equal(new NumberValue(1), global.Get("x", Span(0, 1)));
        Assert.Equal(new NumberValue(3), middle.Get("x", Span(4, 5)));
        Assert.Equal(new NumberValue(3), local.Get("x", Span(8, 9)));
    }

    [Fact]
    public void AssignmentUpdatesOuterVariableWhenNoLocalBindingExists()
    {
        var global = new RuntimeEnvironment();
        var local = new RuntimeEnvironment(global);
        global.Declare("counter", new NumberValue(0), Span(0, 7));

        local.Assign("counter", new NumberValue(1), Span(12, 19));

        Assert.Equal(new NumberValue(1), global.Get("counter", Span(0, 7)));
    }

    [Fact]
    public void AssignmentToShadowingVariableLeavesOuterVariableUnchanged()
    {
        var global = new RuntimeEnvironment();
        var local = new RuntimeEnvironment(global);
        global.Declare("x", new NumberValue(10), Span(0, 1));
        local.Declare("x", new NumberValue(20), Span(5, 6));

        local.Assign("x", new NumberValue(30), Span(10, 11));

        Assert.Equal(new NumberValue(10), global.Get("x", Span(0, 1)));
        Assert.Equal(new NumberValue(30), local.Get("x", Span(5, 6)));
    }

    [Fact]
    public void UnknownLookupReportsSourceAwareRuntimeError()
    {
        var environment = new RuntimeEnvironment();
        var usageSpan = Span(14, 21);

        var error = Assert.Throws<RuntimeError>(() => environment.Get("missing", usageSpan));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
        Assert.Equal(usageSpan, error.Span);
        Assert.Contains("'missing'", error.Message);
    }

    [Fact]
    public void UnknownAssignmentReportsSourceAwareRuntimeError()
    {
        var environment = new RuntimeEnvironment();
        var assignmentSpan = Span(3, 10);

        var error = Assert.Throws<RuntimeError>(() =>
            environment.Assign("missing", new NumberValue(1), assignmentSpan));

        Assert.Equal(DiagnosticCode.UndefinedVariable, error.Code);
        Assert.Equal(assignmentSpan, error.Span);
        Assert.Contains("'missing'", error.Message);
    }

    [Fact]
    public void NothingIsStoredAsVectorValueRatherThanCSharpNull()
    {
        var environment = new RuntimeEnvironment();

        environment.Declare("empty", NothingValue.Instance, Span(0, 5));

        Assert.Same(NothingValue.Instance, environment.Get("empty", Span(0, 5)));
    }

    [Fact]
    public void EnvironmentRejectsCSharpNullValues()
    {
        var environment = new RuntimeEnvironment();

        Assert.Throws<ArgumentNullException>(() =>
            environment.Declare("x", null!, Span(0, 1)));
        Assert.Throws<ArgumentNullException>(() =>
            environment.Assign("x", null!, Span(0, 1)));
    }

    private static SourceSpan Span(int startOffset, int endOffset) =>
        new(
            new SourcePosition(startOffset, 1, startOffset + 1),
            new SourcePosition(endOffset, 1, endOffset + 1));
}
