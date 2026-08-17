using Vector.Core.Runtime.Host;
using Xunit;

namespace Vector.Tests.Runtime;

public sealed class VectorInputHostTests
{
    [Fact]
    public void VectorInputHostForwardsOutputAndReadsConfiguredLines()
    {
        var output = new List<string>();
        var input = new Queue<string?>(new string?[] { "first", null });
        var host = new VectorInputHost(output.Add, () => input.Dequeue());

        host.WriteLine("hello");

        Assert.Equal(new[] { "hello" }, output);
        Assert.Equal("first", host.ReadLine());
        Assert.Null(host.ReadLine());
    }

    [Fact]
    public void VectorInputHostMayDiscardOutputWhileStillProvidingInput()
    {
        var host = new VectorInputHost(null, () => "line");

        host.WriteLine("ignored");

        Assert.Equal("line", host.ReadLine());
    }
}
