# Vector Language Support for Visual Studio Code

This extension provides Vector language editing through the shared C# Vector language server and runs Vector programs through the isolated execution host.

The packaged language server and execution host require an installed .NET 8.x runtime (`Microsoft.NETCore.App` 8.x) with `dotnet` on `PATH`. A .NET 9.x or 10.x runtime alone is not sufficient. Run `dotnet --list-runtimes` to check the installed runtimes.

The extension is under active development. Build, test, packaging, and manual installation instructions are documented in the repository-level VS Code extension guide.
