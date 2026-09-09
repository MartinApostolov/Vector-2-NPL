namespace Vector.Analysis.Modules;

using Vector.Analysis.Documents;
using Vector.Analysis.Symbols;
using Vector.Core.Modules;

public sealed record VectorModuleAnalysis(
    ModuleId Id,
    string FilePath,
    VectorAnalysisResult Analysis,
    IReadOnlyList<VectorSymbol> Members);
