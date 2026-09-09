namespace Vector.VisualStudio;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Settings;

#pragma warning disable VSEXTPREVIEW_SETTINGS
internal static class VectorSettingDefinitions
{
    [VisualStudioContribution]
    internal static SettingCategory VectorSettingsCategory { get; } = new(
        "vector",
        "%Vector.Settings.Category.DisplayName%")
    {
        Description = "%Vector.Settings.Category.Description%",
        GenerateObserverClass = true,
    };

    [VisualStudioContribution]
    internal static Setting.Enum DefaultExecutionEngine { get; } = new(
        "defaultExecutionEngine",
        "%Vector.Settings.DefaultEngine.DisplayName%",
        VectorSettingsCategory,
        [
            new EnumSettingEntry("interpreter", "%Vector.Settings.DefaultEngine.Interpreter%"),
            new EnumSettingEntry("vm", "%Vector.Settings.DefaultEngine.Vm%"),
        ],
        defaultValue: "interpreter")
    {
        Description = "%Vector.Settings.DefaultEngine.Description%",
    };

    [VisualStudioContribution]
    internal static Setting.Boolean LiveDiagnostics { get; } = new(
        "liveDiagnostics",
        "%Vector.Settings.LiveDiagnostics.DisplayName%",
        VectorSettingsCategory,
        defaultValue: true)
    {
        Description = "%Vector.Settings.LiveDiagnostics.Description%",
    };

    [VisualStudioContribution]
    internal static Setting.String ProgramRoot { get; } = new(
        "programRoot",
        "%Vector.Settings.ProgramRoot.DisplayName%",
        VectorSettingsCategory,
        defaultValue: string.Empty)
    {
        Description = "%Vector.Settings.ProgramRoot.Description%",
    };

    [VisualStudioContribution]
    internal static Setting.String PluginPaths { get; } = new(
        "pluginPaths",
        "%Vector.Settings.PluginPaths.DisplayName%",
        VectorSettingsCategory,
        defaultValue: string.Empty)
    {
        Description = "%Vector.Settings.PluginPaths.Description%",
    };
}
#pragma warning restore VSEXTPREVIEW_SETTINGS
