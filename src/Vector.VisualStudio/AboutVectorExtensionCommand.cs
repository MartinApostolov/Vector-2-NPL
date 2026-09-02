namespace Vector.VisualStudio;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;

/// <summary>
/// Minimal smoke command used to verify that the extension loaded correctly.
/// </summary>
[VisualStudioContribution]
public sealed class AboutVectorExtensionCommand : Command
{
    /// <inheritdoc />
    public override CommandConfiguration CommandConfiguration => new("%Vector.VisualStudio.About.DisplayName%")
    {
        TooltipText = "%Vector.VisualStudio.About.Tooltip%",
        Placements = new[] { CommandPlacement.KnownPlacements.ToolsMenu },
    };

    /// <inheritdoc />
    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        await this.Extensibility.Shell().ShowPromptAsync(
            "Vector Language Support is loaded.\n\nCommit 75 provides the Visual Studio extension shell and .vec document registration.",
            PromptOptions.OK,
            cancellationToken);
    }
}
