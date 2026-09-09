namespace Vector.VisualStudio.Settings;

using Microsoft.VisualStudio.Extensibility.Settings;

#pragma warning disable VSEXTPREVIEW_SETTINGS
internal sealed class VectorSettingsService
{
    private readonly VectorSettingsCategoryObserver observer;

    public VectorSettingsService(VectorSettingsCategoryObserver observer)
    {
        this.observer = observer;
        this.observer.Changed += this.OnSettingsChangedAsync;
    }

    public event Func<VectorIdeSettings, Task>? Changed;

    public async Task<VectorIdeSettings> GetAsync(CancellationToken cancellationToken)
    {
        VectorSettingsCategorySnapshot snapshot = await this.observer.GetSnapshotAsync(cancellationToken);
        return FromSnapshot(snapshot);
    }

    private async Task OnSettingsChangedAsync(VectorSettingsCategorySnapshot snapshot)
    {
        if (this.Changed is not { } changed)
        {
            return;
        }

        VectorIdeSettings settings = FromSnapshot(snapshot);
        foreach (Func<VectorIdeSettings, Task> subscriber in changed.GetInvocationList())
        {
            await subscriber(settings).ConfigureAwait(false);
        }
    }

    private static VectorIdeSettings FromSnapshot(VectorSettingsCategorySnapshot snapshot) =>
        VectorIdeSettings.FromRaw(
            snapshot.DefaultExecutionEngine.ValueOrDefault(VectorSettingDefinitions.DefaultExecutionEngine.DefaultValue),
            snapshot.LiveDiagnostics.ValueOrDefault(VectorSettingDefinitions.LiveDiagnostics.DefaultValue),
            snapshot.ProgramRoot.ValueOrDefault(VectorSettingDefinitions.ProgramRoot.DefaultValue),
            snapshot.PluginPaths.ValueOrDefault(VectorSettingDefinitions.PluginPaths.DefaultValue));
}
#pragma warning restore VSEXTPREVIEW_SETTINGS
