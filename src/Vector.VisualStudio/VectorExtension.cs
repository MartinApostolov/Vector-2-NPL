namespace Vector.VisualStudio;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;
using Vector.VisualStudio.Execution;
using Vector.VisualStudio.Output;
using Vector.VisualStudio.Settings;

/// <summary>
/// Entry point for the out-of-process Vector Visual Studio extension.
/// </summary>
[VisualStudioContribution]
public sealed partial class VectorExtension : Extension
{
    /// <summary>
    /// Stable extension identity and metadata used by the generated VSIX.
    /// </summary>
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new(
            id: "Vector.VisualStudio.7e4c1e9c-7699-48fd-b73f-d9fc5ef24ec3",
            version: this.ExtensionAssemblyVersion,
            publisherName: "Martin Apostolov",
            displayName: "Vector Language Support",
            description: "Visual Studio language tooling for the Vector programming language."),
    };

    /// <inheritdoc />
    protected override void InitializeServices(IServiceCollection serviceCollection)
    {
        base.InitializeServices(serviceCollection);
        serviceCollection.AddSettingsObservers();
        serviceCollection.AddSingleton<VectorExecutionClient>();
        serviceCollection.AddSingleton<VectorOutputService>();
        serviceCollection.AddSingleton<VectorSettingsService>();
    }
}
