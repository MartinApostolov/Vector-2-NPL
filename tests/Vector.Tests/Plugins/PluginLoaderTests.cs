using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Plugins;
using Vector.Plugins.Loading;
using Xunit;

namespace Vector.Tests.Plugins;

public sealed class PluginLoaderTests
{
    [Fact]
    public void LoadFromPathLoadsValidExternalPluginAndRegistersItsModule()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);

        var registration = manager.LoadFromPath(Fixture("Vector.TestPlugin.dll"));

        Assert.Equal("fixture.valid", registration.Id);
        Assert.Equal(VectorPluginApi.CurrentVersion, registration.ApiVersion);
        Assert.Equal("fixture.tools", Assert.Single(registration.ModuleIds).QualifiedName);
        Assert.True(registry.TryGet(Id("fixture.tools"), out var definition));
        Assert.NotNull(definition);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LoadFromPathRejectsBlankPath(string path)
    {
        var manager = Manager();

        var error = Assert.Throws<VectorPluginLoadException>(() => manager.LoadFromPath(path));

        Assert.Equal(VectorPluginLoadErrorKind.InvalidPath, error.ErrorKind);
    }

    [Fact]
    public void LoadFromPathRejectsMissingFile()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"vector-missing-{Guid.NewGuid():N}.dll");

        var error = Assert.Throws<VectorPluginLoadException>(() => Manager().LoadFromPath(missing));

        Assert.Equal(VectorPluginLoadErrorKind.FileNotFound, error.ErrorKind);
        Assert.Equal(Path.GetFullPath(missing), error.PluginPath);
    }

    [Fact]
    public void LoadFromPathRejectsNonDllPathBeforeAssemblyLoading()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vector-plugin-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "not a plugin");

        try
        {
            var error = Assert.Throws<VectorPluginLoadException>(() => Manager().LoadFromPath(path));

            Assert.Equal(VectorPluginLoadErrorKind.InvalidExtension, error.ErrorKind);
            Assert.Equal(Path.GetFullPath(path), error.PluginPath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromPathRejectsMalformedDll()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vector-plugin-{Guid.NewGuid():N}.dll");
        File.WriteAllText(path, "this is not a .NET assembly");

        try
        {
            var error = Assert.Throws<VectorPluginLoadException>(() => Manager().LoadFromPath(path));

            Assert.Equal(VectorPluginLoadErrorKind.AssemblyLoadFailure, error.ErrorKind);
            Assert.Equal(Path.GetFullPath(path), error.PluginPath);
            Assert.NotNull(error.InnerException);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromPathRejectsAssemblyWithNoPluginEntryPoint()
    {
        var error = Assert.Throws<VectorPluginLoadException>(() =>
            Manager().LoadFromPath(Fixture("Vector.TestPlugin.NoEntry.dll")));

        Assert.Equal(VectorPluginLoadErrorKind.NoPluginEntryPoint, error.ErrorKind);
    }

    [Fact]
    public void LoadFromPathRejectsAssemblyWithMultiplePluginEntryPoints()
    {
        var error = Assert.Throws<VectorPluginLoadException>(() =>
            Manager().LoadFromPath(Fixture("Vector.TestPlugin.MultipleEntries.dll")));

        Assert.Equal(VectorPluginLoadErrorKind.MultiplePluginEntryPoints, error.ErrorKind);
    }

    [Fact]
    public void LoadFromPathRejectsAbstractPluginEntryPoint()
    {
        var error = Assert.Throws<VectorPluginLoadException>(() =>
            Manager().LoadFromPath(Fixture("Vector.TestPlugin.AbstractEntry.dll")));

        Assert.Equal(VectorPluginLoadErrorKind.InvalidPluginEntryPoint, error.ErrorKind);
    }

    [Fact]
    public void LoadFromPathRejectsEntryPointWithoutPublicParameterlessConstructor()
    {
        var error = Assert.Throws<VectorPluginLoadException>(() =>
            Manager().LoadFromPath(Fixture("Vector.TestPlugin.BadConstructor.dll")));

        Assert.Equal(VectorPluginLoadErrorKind.InvalidPluginEntryPoint, error.ErrorKind);
    }

    [Fact]
    public void LoadFromPathWrapsConstructorFailureWithoutRegisteringPlugin()
    {
        var manager = Manager();

        var error = Assert.Throws<VectorPluginLoadException>(() =>
            manager.LoadFromPath(Fixture("Vector.TestPlugin.ThrowingConstructor.dll")));

        Assert.Equal(VectorPluginLoadErrorKind.ConstructorFailure, error.ErrorKind);
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void LoadedPluginStillUsesRegistrationManagerApiVersionChecks()
    {
        var manager = Manager();

        var error = Assert.Throws<VectorPluginException>(() =>
            manager.LoadFromPath(Fixture("Vector.TestPlugin.ApiMismatch.dll")));

        Assert.Equal(VectorPluginErrorKind.ApiVersionMismatch, error.ErrorKind);
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void LoadedPluginStillUsesTransactionalRegistrationFailureHandling()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);

        var error = Assert.Throws<VectorPluginException>(() =>
            manager.LoadFromPath(Fixture("Vector.TestPlugin.RegistrationFailure.dll")));

        Assert.Equal(VectorPluginErrorKind.RegistrationFailure, error.ErrorKind);
        Assert.False(registry.TryGet(Id("fixture.staged"), out _));
        Assert.Empty(manager.Registrations);
    }

    private static VectorPluginManager Manager() => new(new NativeModuleRegistry());

    private static string Fixture(string assemblyName) =>
        Path.Combine(AppContext.BaseDirectory, assemblyName);

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));
}
