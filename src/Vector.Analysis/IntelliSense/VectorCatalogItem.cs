namespace Vector.Analysis.IntelliSense;

public enum VectorCatalogItemKind
{
    Keyword,
    Function,
    Module,
    Constant,
    Variable,
    Parameter,
}

public sealed record VectorCatalogItem(
    string Label,
    VectorCatalogItemKind Kind,
    string Detail,
    string Documentation);

public sealed record VectorCallableDescriptor(
    string Name,
    IReadOnlyList<string> Parameters,
    string Documentation)
{
    public string Signature => $"{this.Name}({string.Join(", ", this.Parameters)})";
}

public sealed record VectorModuleDescriptor(
    string QualifiedName,
    string Documentation,
    IReadOnlyList<VectorCallableDescriptor> Functions,
    IReadOnlyList<VectorCatalogItem> Constants);
