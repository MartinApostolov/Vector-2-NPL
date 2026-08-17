using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.StandardLibrary;
using Vector.Plugins;
using Xunit;

namespace Vector.Tests.Plugins;

public sealed class PluginRegistrationTests
{
    [Fact]
    public void RegisterCommitsValidPluginModule()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);
        var plugin = Plugin("example.plugin", Definition("example.tools"));

        var registration = manager.Register(plugin);

        Assert.True(registry.TryGet(Id("example.tools"), out var definition));
        Assert.NotNull(definition);
        Assert.Equal("example.plugin", registration.Id);
    }

    [Fact]
    public void RegisterCommitsSeveralModulesFromOnePlugin()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);
        var plugin = Plugin(
            "example.plugin",
            Definition("example.tools"),
            Definition("example.text"),
            Definition("example.game"));

        var registration = manager.Register(plugin);

        Assert.Equal(3, registration.ModuleIds.Count);
        Assert.True(registry.TryGet(Id("example.tools"), out _));
        Assert.True(registry.TryGet(Id("example.text"), out _));
        Assert.True(registry.TryGet(Id("example.game"), out _));
    }

    [Fact]
    public void RegisterRejectsDuplicatePluginIdBeforeCallingPluginAgain()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);
        manager.Register(Plugin("example.plugin", Definition("example.first")));
        var duplicate = new DelegatePlugin(
            "example.plugin",
            VectorPluginApi.CurrentVersion,
            _ => throw new InvalidOperationException("should not run"));

        var error = Assert.Throws<VectorPluginException>(() => manager.Register(duplicate));

        Assert.Equal(VectorPluginErrorKind.DuplicatePlugin, error.ErrorKind);
        Assert.Equal("example.plugin", error.PluginId);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void PluginIdsAreComparedCaseSensitively()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);

        manager.Register(Plugin("example.plugin", Definition("example.lower")));
        manager.Register(Plugin("Example.Plugin", Definition("example.upper")));

        Assert.Equal(2, manager.Registrations.Count);
    }

    [Fact]
    public void RegisterRejectsApiVersionMismatchWithoutInstallingModules()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);
        var plugin = new DelegatePlugin(
            "example.plugin",
            VectorPluginApi.CurrentVersion + 1,
            context => context.RegisterModule(Definition("example.tools")));

        var error = Assert.Throws<VectorPluginException>(() => manager.Register(plugin));

        Assert.Equal(VectorPluginErrorKind.ApiVersionMismatch, error.ErrorKind);
        Assert.False(registry.TryGet(Id("example.tools"), out _));
        Assert.Empty(manager.Registrations);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterRejectsBlankPluginId(string pluginId)
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);
        var plugin = Plugin(pluginId, Definition("example.tools"));

        var error = Assert.Throws<VectorPluginException>(() => manager.Register(plugin));

        Assert.Equal(VectorPluginErrorKind.InvalidPluginId, error.ErrorKind);
        Assert.False(registry.TryGet(Id("example.tools"), out _));
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void RegisterMapsDuplicateModuleInsidePluginAndCommitsNothing()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);
        var plugin = Plugin(
            "example.plugin",
            Definition("example.tools"),
            Definition("example.tools"));

        var error = Assert.Throws<VectorPluginException>(() => manager.Register(plugin));

        Assert.Equal(VectorPluginErrorKind.DuplicateModule, error.ErrorKind);
        Assert.Equal("example.tools", error.ModuleId?.QualifiedName);
        Assert.False(registry.TryGet(Id("example.tools"), out _));
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void RegisterRejectsDuplicateModuleEvenIfPluginCatchesTheStagingException()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);
        var plugin = new DelegatePlugin(
            "example.plugin",
            VectorPluginApi.CurrentVersion,
            context =>
            {
                context.RegisterModule(Definition("example.tools"));

                try
                {
                    context.RegisterModule(Definition("example.tools"));
                }
                catch (InvalidOperationException)
                {
                    // A plugin cannot suppress duplicate-module validation by swallowing
                    // the staging-context exception.
                }

                context.RegisterModule(Definition("example.other"));
            });

        var error = Assert.Throws<VectorPluginException>(() => manager.Register(plugin));

        Assert.Equal(VectorPluginErrorKind.DuplicateModule, error.ErrorKind);
        Assert.Equal("example.tools", error.ModuleId?.QualifiedName);
        Assert.False(registry.TryGet(Id("example.tools"), out _));
        Assert.False(registry.TryGet(Id("example.other"), out _));
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void RegisterRejectsConflictWithStandardModuleAndCommitsNothing()
    {
        var registry = StandardLibraryRegistry.CreateDefault();
        var manager = new VectorPluginManager(registry);
        var plugin = Plugin(
            "example.plugin",
            Definition("example.safe"),
            Definition("lib.math"));

        var error = Assert.Throws<VectorPluginException>(() => manager.Register(plugin));

        Assert.Equal(VectorPluginErrorKind.ModuleConflict, error.ErrorKind);
        Assert.Equal("lib.math", error.ModuleId?.QualifiedName);
        Assert.False(registry.TryGet(Id("example.safe"), out _));
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void RegisterRejectsConflictWithEarlierPluginAndCommitsNothingFromSecondPlugin()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);
        manager.Register(Plugin("first.plugin", Definition("example.shared")));
        var second = Plugin(
            "second.plugin",
            Definition("example.second"),
            Definition("example.shared"));

        var error = Assert.Throws<VectorPluginException>(() => manager.Register(second));

        Assert.Equal(VectorPluginErrorKind.ModuleConflict, error.ErrorKind);
        Assert.Equal("second.plugin", error.PluginId);
        Assert.False(registry.TryGet(Id("example.second"), out _));
        Assert.Single(manager.Registrations);
        Assert.Equal("first.plugin", manager.Registrations[0].Id);
    }

    [Fact]
    public void RegisterWrapsPluginRegistrationFailureAndCommitsNothing()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);
        var cause = new InvalidOperationException("plugin exploded");
        var plugin = new DelegatePlugin(
            "example.plugin",
            VectorPluginApi.CurrentVersion,
            context =>
            {
                context.RegisterModule(Definition("example.staged"));
                throw cause;
            });

        var error = Assert.Throws<VectorPluginException>(() => manager.Register(plugin));

        Assert.Equal(VectorPluginErrorKind.RegistrationFailure, error.ErrorKind);
        Assert.Same(cause, error.InnerException);
        Assert.False(registry.TryGet(Id("example.staged"), out _));
        Assert.Empty(manager.Registrations);
    }

    [Fact]
    public void FailedPluginCanBeRetriedAfterFailureBecauseItWasNotMarkedRegistered()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);
        var attempts = 0;
        var plugin = new DelegatePlugin(
            "example.plugin",
            VectorPluginApi.CurrentVersion,
            context =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new InvalidOperationException("first attempt fails");
                }

                context.RegisterModule(Definition("example.tools"));
            });

        Assert.Throws<VectorPluginException>(() => manager.Register(plugin));
        var registration = manager.Register(plugin);

        Assert.Equal(2, attempts);
        Assert.Equal("example.plugin", registration.Id);
        Assert.True(registry.TryGet(Id("example.tools"), out _));
        Assert.Single(manager.Registrations);
    }

    [Fact]
    public void SuccessfulRegistrationMetadataIsExposedInRegistrationOrder()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);

        var first = manager.Register(Plugin(
            "first.plugin",
            Definition("first.tools"),
            Definition("first.text")));
        var second = manager.Register(Plugin(
            "second.plugin",
            Definition("second.tools")));

        Assert.Equal(2, manager.Registrations.Count);
        Assert.Same(first, manager.Registrations[0]);
        Assert.Same(second, manager.Registrations[1]);
        Assert.Equal(VectorPluginApi.CurrentVersion, first.ApiVersion);
        Assert.Equal(new[] { "first.tools", "first.text" }, first.ModuleIds.Select(id => id.QualifiedName).ToArray());
        Assert.Equal(new[] { "second.tools" }, second.ModuleIds.Select(id => id.QualifiedName).ToArray());
    }

    private static DelegatePlugin Plugin(string id, params NativeModuleDefinition[] definitions) =>
        new(
            id,
            VectorPluginApi.CurrentVersion,
            context =>
            {
                foreach (var definition in definitions)
                {
                    context.RegisterModule(definition);
                }
            });

    private static NativeModuleDefinition Definition(string qualifiedName) =>
        new(Id(qualifiedName), _ => { });

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private sealed class DelegatePlugin : IVectorPlugin
    {
        private readonly Action<IVectorPluginContext> _register;

        internal DelegatePlugin(string id, int apiVersion, Action<IVectorPluginContext> register)
        {
            Id = id;
            ApiVersion = apiVersion;
            _register = register;
        }

        public string Id { get; }

        public int ApiVersion { get; }

        public void Register(IVectorPluginContext context) => _register(context);
    }
}
