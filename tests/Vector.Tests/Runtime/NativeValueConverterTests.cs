using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;
using Xunit;

namespace Vector.Tests.Runtime;

public sealed class NativeValueConverterTests
{
    [Fact]
    public void NumberConversionRoundTripsFiniteDouble()
    {
        var vector = NativeValueConverter.FromNumber(12.5);

        Assert.Equal(12.5, NativeValueConverter.ToNumber(vector));
    }

    [Fact]
    public void TextConversionRoundTripsString()
    {
        var vector = NativeValueConverter.FromText("Vector");

        Assert.Equal("Vector", NativeValueConverter.ToText(vector));
    }

    [Fact]
    public void BooleanConversionRoundTripsBool()
    {
        var vector = NativeValueConverter.FromBoolean(true);

        Assert.True(NativeValueConverter.ToBoolean(vector));
    }

    [Fact]
    public void ListConversionUsesControlledVectorValueView()
    {
        var list = new ListValue(new VectorValue[]
        {
            new NumberValue(1),
            new TextValue("two")
        });

        var view = NativeValueConverter.ToList(list);

        Assert.Same(list.Elements, view);
        Assert.Equal(new NumberValue(1), view[0]);
        Assert.Equal(new TextValue("two"), view[1]);
    }

    [Fact]
    public void HostListConversionCreatesVectorList()
    {
        var list = NativeValueConverter.FromList(new VectorValue[]
        {
            new BooleanValue(true),
            NothingValue.Instance
        });

        Assert.Equal(
            new ListValue(new VectorValue[] { new BooleanValue(true), NothingValue.Instance }),
            list);
    }

    [Fact]
    public void NothingUsesExplicitSingletonBridge()
    {
        var nothing = NativeValueConverter.FromNothing();

        Assert.Same(NothingValue.Instance, nothing);
        Assert.Same(NothingValue.Instance, NativeValueConverter.ToNothing(nothing));
    }

    [Fact]
    public void NullableTextBridgeMapsCSharpNullToVectorNothing()
    {
        Assert.Same(NothingValue.Instance, NativeValueConverter.FromNullableText(null));
        Assert.Null(NativeValueConverter.ToNullableText(NothingValue.Instance));
        Assert.Equal("value", NativeValueConverter.ToNullableText(new TextValue("value")));
    }

    [Fact]
    public void UnsupportedInputConversionFailsExplicitly()
    {
        var error = Assert.Throws<NativeRuntimeException>(() =>
            NativeValueConverter.ToNumber(new TextValue("12"), "amount"));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("amount", error.Message);
        Assert.Contains("text", error.Message);
    }

    [Fact]
    public void NonFiniteVectorNumberCannotCrossIntoNativeCode()
    {
        var error = Assert.Throws<NativeRuntimeException>(() =>
            NativeValueConverter.ToNumber(new NumberValue(double.NaN), "value"));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("finite number", error.Message);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteHostNumberCannotBecomeVectorNumber(double value)
    {
        var error = Assert.Throws<NativeRuntimeException>(() =>
            NativeValueConverter.FromNumber(value));

        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, error.Code);
        Assert.Contains("non-finite", error.Message);
    }

    [Fact]
    public void FromListRejectsNestedNonFiniteVectorNumbers()
    {
        var error = Assert.Throws<NativeRuntimeException>(() =>
            NativeValueConverter.FromList(new VectorValue[]
            {
                new ListValue(new VectorValue[] { new NumberValue(double.PositiveInfinity) })
            }));

        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, error.Code);
    }
}
