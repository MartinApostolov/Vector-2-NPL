# Vector Visual Studio extension

The Vector Visual Studio extension v1 targets **Visual Studio Community 2026** on 64-bit Windows. It uses the VisualStudio.Extensibility out-of-process model and is packaged as a VSIX. The manifest accepts Visual Studio 17.14 or newer so the same package can load in the current 2026 release while retaining the SDK's supported minimum.

## Features

Opening a `.vec` file activates:

- TextMate syntax highlighting;
- line and block comments, brackets, auto-closing pairs, and surrounding-pair behavior;
- live lexer/parser diagnostics in the editor and Error List;
- completion for keywords, built-ins, standard modules, user declarations, parameters, lexical scopes, imported local modules, and qualified module members;
- Markdown hover information;
- hierarchical document symbols;
- Go To Definition for source declarations, imports, and local-module members;
- signature help for built-ins, standard-library functions, user functions, and local-module functions;
- Find All References for statically resolved source variables, parameters, functions, loop variables, and local-module members;
- conservative Rename for those same source symbols, with identifier and binding-conflict validation.

The extension also contributes these commands to the **Extensions** menu when a Vector editor is active:

- **Vector: Run Current File** — uses the configured default engine;
- **Vector: Run Current File with Interpreter**;
- **Vector: Run Current File with VM**;
- **Vector: Show Bytecode** — compiles and disassembles without running the program.

All commands use the current in-memory editor buffer, including unsaved changes. Results, program output, diagnostics, disassembly, and host failures are written to the **Vector** Output channel.

## Prerequisites

For development and packaging:

- Windows x64 or ARM64;
- .NET 8 SDK or newer SDK capable of targeting .NET 8;
- Visual Studio Community 2026 with the core editor and .NET development tooling;
- NuGet access for the first restore.

The extension itself runs out of process on .NET 8. A debugger, custom `.vecproj` project system, Vector package manager, VS Code client, and NPL translator are not included in this phase.

## Architecture and safety boundary

```text
Visual Studio Community 2026
          |
          +-- Vector.VisualStudio (thin out-of-process shell)
          |       |
          |       +-- stdio LSP --> Vector.LanguageServer
          |       |                    |
          |       |                    +--> Vector.Analysis --> Vector.Core parser
          |       |
          |       +-- explicit run --> Vector.ExecutionHost
          |                                |
          |                                +--> Vector.Core / Vector.Plugins
          |
          +-- editor UI: diagnostics, completion, navigation, Output
```

`Vector.VisualStudio` owns only Visual Studio registration, settings, command plumbing, server/host process startup, and Output integration. Language intelligence lives in `Vector.Analysis` and is exposed through the editor-independent `Vector.LanguageServer`. Execution uses the separate `Vector.ExecutionHost` process and the neutral `Vector.ExecutionProtocol` contract.

Static editor operations never execute a Vector program and never load plugin DLLs. Plugin paths are accepted only by explicit run commands and are sent to `Vector.ExecutionHost`. Plugins are trusted .NET code and are not sandboxed, so configure only DLLs you trust. **Show Bytecode** deliberately suppresses configured plugins and never executes source side effects.

This separation also preserves two future reuse paths:

- a VS Code extension can connect to the same language server and execution host;
- a future editor-independent `Vector.Npl` component can generate visible in-memory Vector source, pass it through `Vector.Analysis`, and leave execution as an explicit user action.

Neither VS Code nor NPL is implemented on this branch.

## Settings

The extension registers a **Vector** category in Visual Studio settings:

| Setting | Default | Meaning |
|---|---:|---|
| Default execution engine | Interpreter | Engine used by **Vector: Run Current File**. Explicit Interpreter/VM commands override it. |
| Live diagnostics | On | Publishes lexer/parser diagnostics while editing. Changing it restarts the LSP connection. |
| Program root | Auto | Empty infers the root from the current file. An explicit existing directory drives local-module analysis and execution. |
| Execution plugin paths | Empty | Semicolon- or newline-separated trusted DLLs passed only to explicit run requests. |

An invalid explicit program root stops the run before a process is launched and produces a settings error in the Vector Output channel. Invalid plugin paths are reported as structured execution-host failures.

## Build, test, and package

From the repository root, the complete repeatable workflow is:

```powershell
dotnet restore Vector.sln
dotnet build Vector.sln --configuration Release
dotnet test Vector.sln --no-build --configuration Release
```

Building `src/Vector.VisualStudio/Vector.VisualStudio.csproj` produces:

```text
src/Vector.VisualStudio/bin/Release/net8.0-windows8.0/Vector.VisualStudio.vsix
```

The repository also provides a packaging gate that restores, builds, tests, and inspects all required VSIX entries:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-VisualStudioVsix.ps1
```

Use `-SkipRestore` only after dependencies are already restored. The script leaves all generated files under ignored `bin`/`obj` directories and prints the final VSIX path and SHA-256 hash.

The package audit requires the extension metadata, grammar, language configuration, `Vector.LanguageServer` with `Vector.Analysis`, and `Vector.ExecutionHost` with `Vector.ExecutionProtocol`, `Vector.Core`, and `Vector.Plugins`.

## Experimental Instance workflow

Automated tests exercise the shared analysis, real stdio language-server process, real execution-host process, generated VSIX metadata, and packaged payload. Graphical behavior still needs an installed Visual Studio instance:

1. Open `Vector.sln` in Visual Studio Community 2026.
2. Select `Vector.VisualStudio` as the startup project and start debugging to open the Experimental Instance.
3. Open `examples/visual-studio-acceptance/main.vec` and follow [the acceptance checklist](../examples/visual-studio-acceptance/README.md).
4. Confirm diagnostics in the Error List/editor, IntelliSense UI, navigation, settings, commands, and Vector Output presentation.
5. Close the Experimental Instance and verify the language-server and execution-host child processes do not remain running.

This repository does not claim that graphical checklist was executed by automated tooling.

To install a Release package outside the Experimental Instance, close Visual Studio, open `Vector.VisualStudio.vsix`, accept the VSIX Installer prompt for the intended Community instance, and restart Visual Studio. Uninstall or update it through **Extensions > Manage Extensions**.

## Troubleshooting

- **No Vector language features:** confirm the file ends in `.vec`, the extension is enabled, and `LanguageServer/Vector.LanguageServer.exe` exists in the installed payload. Check the Visual Studio Activity Log and trace output for server startup errors.
- **Commands are disabled:** make a `.vec` editor the active document. Commands intentionally apply only to the registered Vector content type.
- **Local module is missing:** leave Program root empty when modules live beneath the current file's directory, or configure the existing directory that contains the module's qualified path.
- **No live squiggles:** confirm Live diagnostics is enabled. Toggling analysis settings restarts the server connection; reopening the document/Experimental Instance is a useful manual recovery check.
- **Run fails before execution:** inspect the Vector Output channel for a missing host, timeout, invalid root, invalid plugin, or plugin registration failure.
- **Plugin completion is absent:** this is intentional. User plugins execute only through an explicit run and are never loaded for static analysis.
- **NuGet `NU1900` during an offline build:** this means vulnerability metadata could not be downloaded. It is not a compiler/test failure, but connected release environments should restore again so auditing can complete.

## Known limitations

- There is no integrated debugger or breakpoint support.
- There is no custom Vector project system; folders and ordinary files provide workspace context.
- Rename and references are intentionally limited to statically resolved Vector source symbols. Dynamic/unresolved names, built-ins, standard-library members, and plugin members are not renamed.
- Local-module discovery is file-system based and scoped to the inferred/configured program root.
- Formatting, code actions, semantic tokens, package management, Marketplace publishing automation, VS Code, and NPL are not implemented here.
- Visual presentation and installation must be checked manually in Visual Studio Community 2026.
