namespace Vector.VisualStudio.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Vector.ExecutionProtocol;
using Vector.VisualStudio.Execution;
using Vector.VisualStudio.Output;

[VisualStudioContribution]
internal sealed class RunVectorCommand(VectorExecutionClient client, VectorOutputService output)
    : VectorExecutionCommand(client, output)
{
    public override CommandConfiguration CommandConfiguration => new("%Vector.Commands.Run.DisplayName%")
    {
        TooltipText = "%Vector.Commands.Run.Tooltip%",
        Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
        EnabledWhen = ActivationConstraint.ClientContext(
            ClientContextKey.Shell.ActiveEditorContentType,
            VectorExtension.VectorDocumentTypeName),
    };

    protected override VectorExecutionEngine Engine => VectorExecutionEngine.Interpreter;
}
