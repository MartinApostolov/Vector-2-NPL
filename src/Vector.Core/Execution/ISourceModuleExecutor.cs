using Vector.Core.Modules;
using Vector.Core.Runtime.Host;

namespace Vector.Core.Execution;

/// <summary>
/// Executes the parsed top-level code for one loaded Vector source module.
/// Module resolution, caching, and one-time initialization remain owned by <see cref="ModuleLoader"/>.
/// </summary>
internal interface ISourceModuleExecutor
{
    void Execute(LoadedModule module, IVectorHost host, ModuleLoader moduleLoader);
}
