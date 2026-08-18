using Vector.Core.Modules;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;

namespace Vector.Core.Execution;

/// <summary>
/// Preserves the historical ModuleLoader behavior by executing source modules with the interpreter.
/// </summary>
internal sealed class InterpreterSourceModuleExecutor : ISourceModuleExecutor
{
    public void Execute(LoadedModule module, IVectorHost host, ModuleLoader moduleLoader)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(moduleLoader);

        var sourceData = module.SourceData
            ?? throw new InvalidOperationException(
                $"Source module '{module.Id}' does not contain source metadata.");

        var interpreter = new Interpreter(module.Environment, host, moduleLoader);
        interpreter.Execute(sourceData.Syntax, sourceData.FilePath, sourceData.Source);
    }
}
