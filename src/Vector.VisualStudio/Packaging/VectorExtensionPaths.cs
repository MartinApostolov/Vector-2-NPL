namespace Vector.VisualStudio.Packaging;

internal sealed class VectorExtensionPaths
{
    public VectorExtensionPaths()
        : this(typeof(VectorExtensionPaths).Assembly.Location)
    {
    }

    internal VectorExtensionPaths(string extensionAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(extensionAssemblyPath))
        {
            throw new ArgumentException("The Vector extension assembly path is unavailable.", nameof(extensionAssemblyPath));
        }

        this.ExtensionAssemblyPath = Path.GetFullPath(extensionAssemblyPath);
        this.ExtensionDirectory = Path.GetDirectoryName(this.ExtensionAssemblyPath)
            ?? throw new InvalidOperationException(
                $"The Vector extension installation directory could not be resolved from '{this.ExtensionAssemblyPath}'.");
    }

    public string ExtensionAssemblyPath { get; }

    public string ExtensionDirectory { get; }

    public string LanguageServerPath => this.Resolve("LanguageServer", "Vector.LanguageServer.exe");

    public string ExecutionHostPath => this.Resolve("ExecutionHost", "Vector.ExecutionHost.exe");

    private string Resolve(params string[] relativeSegments) => Path.GetFullPath(
        Path.Combine([this.ExtensionDirectory, .. relativeSegments]));
}
