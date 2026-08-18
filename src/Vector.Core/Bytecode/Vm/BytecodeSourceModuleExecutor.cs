using Vector.Core.Bytecode.Compiler;
using Vector.Core.Execution;
using Vector.Core.Modules;
using Vector.Core.Runtime.Host;

namespace Vector.Core.Bytecode.Vm;

/// <summary>
/// Compiles and executes local Vector source modules with the bytecode VM while reusing
/// the module's persistent environment and the program's existing ModuleLoader.
/// </summary>
internal sealed class BytecodeSourceModuleExecutor : ISourceModuleExecutor
{
    public void Execute(LoadedModule module, IVectorHost host, ModuleLoader moduleLoader)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(moduleLoader);

        var sourceData = module.SourceData
            ?? throw new InvalidOperationException(
                $"Source module '{module.Id}' does not contain source metadata.");

        var compilation = new BytecodeCompiler().Compile(
            sourceData.Syntax,
            sourceData.FilePath,
            sourceData.Source);

        new VectorVirtualMachine(module.Environment, host, moduleLoader)
            .Execute(compilation.Program);
    }
}
