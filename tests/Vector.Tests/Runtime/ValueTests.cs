using Vector.Core.Runtime.Values;
using Xunit;

namespace Vector.Tests.Runtime;

public sealed class ValueTests
{
    public static TheoryData<VectorValue, VectorValueKind, string> ValueKinds => new()
    {
        { new NumberValue(1), VectorValueKind.Number, "number" },
        { new TextValue("hello"), VectorValueKind.Text, "text" },
        { new BooleanValue(true), VectorValueKind.Boolean, "boolean" },
        { new ListValue(), VectorValueKind.List, "list" },
        { new TestFunctionValue(), VectorValueKind.Function, "function" },
        { NothingValue.Instance, VectorValueKind.Nothing, "nothing" }
    };

    [Theory]
    [MemberData(nameof(ValueKinds))]
    public void ValuesExposeRuntimeKindAndLanguageTypeName(
        VectorValue value,
        VectorValueKind expectedKind,
        string expectedTypeName)
    {
        Assert.Equal(expectedKind, value.Kind);
        Assert.Equal(expectedTypeName, value.TypeName);
    }

    [Fact]
    public void ScalarValuesCompareByValue()
    {
        Assert.Equal(new NumberValue(12.5), new NumberValue(12.5));
        Assert.NotEqual(new NumberValue(12.5), new NumberValue(12.6));

        Assert.Equal(new TextValue("Vector"), new TextValue("Vector"));
        Assert.NotEqual(new TextValue("Vector"), new TextValue("vector"));

        Assert.Equal(new BooleanValue(true), new BooleanValue(true));
        Assert.NotEqual(new BooleanValue(true), new BooleanValue(false));
    }

    [Fact]
    public void DifferentRuntimeKindsCompareUnequal()
    {
        VectorValue number = new NumberValue(1);
        VectorValue text = new TextValue("1");
        VectorValue boolean = new BooleanValue(true);

        Assert.NotEqual(number, text);
        Assert.NotEqual(number, boolean);
        Assert.NotEqual(text, boolean);
        Assert.NotEqual(number, NothingValue.Instance);
    }

    [Fact]
    public void NothingIsASingletonValue()
    {
        Assert.Same(NothingValue.Instance, NothingValue.Instance);
        Assert.Equal(VectorValueKind.Nothing, NothingValue.Instance.Kind);
        Assert.Equal(NothingValue.Instance, NothingValue.Instance);
    }

    [Fact]
    public void ListsCompareContentsRecursively()
    {
        var left = new ListValue(new VectorValue[]
        {
            new NumberValue(1),
            new ListValue(new VectorValue[]
            {
                new TextValue("two"),
                new BooleanValue(true),
                NothingValue.Instance
            })
        });

        var right = new ListValue(new VectorValue[]
        {
            new NumberValue(1),
            new ListValue(new VectorValue[]
            {
                new TextValue("two"),
                new BooleanValue(true),
                NothingValue.Instance
            })
        });

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ListEqualityDetectsOrderLengthAndNestedDifferences()
    {
        var original = new ListValue(new VectorValue[]
        {
            new NumberValue(1),
            new ListValue(new VectorValue[] { new NumberValue(2) })
        });

        var reordered = new ListValue(new VectorValue[]
        {
            new ListValue(new VectorValue[] { new NumberValue(2) }),
            new NumberValue(1)
        });

        var longer = new ListValue(new VectorValue[]
        {
            new NumberValue(1),
            new ListValue(new VectorValue[] { new NumberValue(2) }),
            new NumberValue(3)
        });

        var nestedDifference = new ListValue(new VectorValue[]
        {
            new NumberValue(1),
            new ListValue(new VectorValue[] { new NumberValue(99) })
        });

        Assert.NotEqual(original, reordered);
        Assert.NotEqual(original, longer);
        Assert.NotEqual(original, nestedDifference);
    }

    [Fact]
    public void EmptyAndAllNumberListsAreNumericLists()
    {
        Assert.True(new ListValue().IsNumericList);
        Assert.True(new ListValue(new VectorValue[]
        {
            new NumberValue(1),
            new NumberValue(2.5)
        }).IsNumericList);
    }

    [Fact]
    public void NumericListStatusReflectsCurrentContents()
    {
        var list = new ListValue(new VectorValue[]
        {
            new NumberValue(1),
            new NumberValue(2)
        });

        Assert.True(list.IsNumericList);

        list[1] = new TextValue("two");
        Assert.False(list.IsNumericList);

        list[1] = new NumberValue(2);
        Assert.True(list.IsNumericList);
    }

    [Fact]
    public void FunctionValuesCompareByIdentity()
    {
        var first = new TestFunctionValue();
        var second = new TestFunctionValue();
        VectorValue sameReference = first;

        Assert.Equal(first, sameReference);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void RecursiveListEqualityUsesFunctionIdentity()
    {
        var sharedFunction = new TestFunctionValue();
        var left = new ListValue(new VectorValue[] { sharedFunction });
        var sameFunction = new ListValue(new VectorValue[] { sharedFunction });
        var differentFunction = new ListValue(new VectorValue[] { new TestFunctionValue() });

        Assert.Equal(left, sameFunction);
        Assert.NotEqual(left, differentFunction);
    }

    private sealed class TestFunctionValue : FunctionValue
    {
    }
}
