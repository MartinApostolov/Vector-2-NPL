namespace Vector.VisualStudio.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Vector.ExecutionProtocol;
using Vector.VisualStudio.Execution;
using Vector.VisualStudio.Output;

[VisualStudioContribution]
internal sealed class DisassembleVectorCommand(VectorExecutionClient client, VectorOutputService output)
    : VectorExecutionCommand(client, output)
{
    public override CommandConfiguration CommandConfiguration => new("%Vector.Commands.Disassemble.DisplayName%")
    {
        TooltipText = "%Vector.Commands.Disassemble.Tooltip%",
        Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
        EnabledWhen = ActivationConstraint.ClientContext(
            ClientContextKey.Shell.ActiveEditorContentType,
            VectorExtension.VectorDocumentTypeName),
    };

    protected override VectorExecutionEngine Engine => VectorExecutionEngine.Vm;

    protected override VectorExecutionOperation Operation => VectorExecutionOperation.Disassemble;
}
