namespace Vector.Analysis.IntelliSense;

public static class VectorLanguageCatalog
{
    public static IReadOnlyList<VectorCatalogItem> Keywords { get; } =
    [
        Keyword("let", "Declares a lexical variable with an initializer."),
        Keyword("if", "Starts a conditional statement."),
        Keyword("else", "Provides the alternate branch of a conditional."),
        Keyword("while", "Repeats a block while its condition is true."),
        Keyword("for", "Iterates over the values in a list."),
        Keyword("in", "Separates a for-loop variable from its iterable."),
        Keyword("function", "Declares a Vector function."),
        Keyword("return", "Returns a value from the current function."),
        Keyword("break", "Exits the nearest loop."),
        Keyword("continue", "Continues the nearest loop."),
        Keyword("true", "The boolean true literal."),
        Keyword("false", "The boolean false literal."),
        Keyword("nothing", "The Vector nothing value."),
        Keyword("and", "Short-circuit logical conjunction."),
        Keyword("or", "Short-circuit logical disjunction."),
        Keyword("not", "Logical negation."),
        Keyword("import", "Imports a qualified Vector module."),
    ];

    public static IReadOnlyList<VectorCallableDescriptor> Builtins { get; } =
    [
        Function("print", ["value"], "Writes one formatted value followed by a newline."),
        Function("length", ["value"], "Returns the length of text or a list."),
        Function("concat", ["left", "right"], "Concatenates two text values or two lists."),
        Function("text", ["value"], "Converts a Vector value to text."),
        Function("number", ["value"], "Converts a finite numeric text value to a number."),
        Function("type", ["value"], "Returns the runtime type name of a value."),
        Function("range", ["start", "end"], "Creates a numeric list from start (inclusive) to end (exclusive)."),
    ];

    public static IReadOnlyList<VectorModuleDescriptor> StandardModules { get; } =
    [
        Module("lib.math", "Scalar mathematical constants and functions.",
            [
                Function("abs", ["value"], "Returns the absolute value."),
                Function("sqrt", ["value"], "Returns the square root."),
                Function("min", ["a", "b"], "Returns the smaller number."),
                Function("max", ["a", "b"], "Returns the larger number."),
                Function("pow", ["value", "exponent"], "Raises a number to a power."),
            ],
            [Constant("pi", "The mathematical constant π."), Constant("e", "Euler's number.")]),
        Module("lib.collections", "Aggregate operations over numeric lists.",
            [
                Function("sum", ["values"], "Returns the sum of a numeric list."),
                Function("min", ["values"], "Returns the smallest value in a non-empty numeric list."),
                Function("max", ["values"], "Returns the largest value in a non-empty numeric list."),
            ]),
        Module("lib.io", "Host input operations.",
            [Function("readLine", [], "Reads one line from an input-capable host.")]),
        Module("lib.vector", "Vector mathematics over finite numeric lists.",
            [
                Function("dot", ["a", "b"], "Returns the dot product of equal-length vectors."),
                Function("magnitude", ["v"], "Returns a vector's Euclidean magnitude."),
                Function("normalize", ["v"], "Returns the normalized form of a non-zero vector."),
            ]),
        Module("lib.matrix", "Matrix operations over rectangular numeric lists.",
            [
                Function("shape", ["matrix"], "Returns a matrix's row and column counts."),
                Function("transpose", ["matrix"], "Returns the matrix transpose."),
                Function("add", ["a", "b"], "Adds equal-shaped matrices."),
                Function("multiply", ["a", "b"], "Multiplies compatible matrices."),
            ]),
    ];

    private static VectorCatalogItem Keyword(string label, string documentation) =>
        new(label, VectorCatalogItemKind.Keyword, "Vector keyword", documentation);

    private static VectorCatalogItem Constant(string label, string documentation) =>
        new(label, VectorCatalogItemKind.Constant, "Vector constant", documentation);

    private static VectorCallableDescriptor Function(string name, string[] parameters, string documentation) =>
        new(name, parameters, documentation);

    private static VectorModuleDescriptor Module(
        string name,
        string documentation,
        VectorCallableDescriptor[] functions,
        VectorCatalogItem[]? constants = null) =>
        new(name, documentation, functions, constants ?? []);
}
