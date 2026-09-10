# Vector Visual Studio Code extension

The Vector VS Code extension is a thin TypeScript client around the shared C# language and execution infrastructure. It uses the official `vscode-languageclient` package over stdio, launches `Vector.LanguageServer.dll` and `Vector.ExecutionHost.dll` through `dotnet`, and bundles the extension client with esbuild. The TextMate grammar and language configuration are synchronized with the Visual Studio extension and guarded by tests.

The packaging layout follows the current official VS Code guidance for [language server extensions](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide), [bundled extensions](https://code.visualstudio.com/api/working-with-extensions/bundling-extension), [extension testing](https://code.visualstudio.com/api/working-with-extensions/testing-extension), and [VSIX packaging with `vsce`](https://code.visualstudio.com/api/working-with-extensions/publishing-extension).

## Prerequisites

- .NET 8.x runtime (`Microsoft.NETCore.App` 8.x) with `dotnet` on `PATH`. A .NET 9.x or 10.x runtime alone is not sufficient for the current framework-dependent `net8.0` payload. Check installed runtimes with `dotnet --list-runtimes`.
- .NET 8 SDK or newer to build and test the repository.
- Node.js 20 or newer and npm to build/package the extension.
- Visual Studio Code 1.104.0 or newer for development-host tests and manual testing.

The VSIX includes all managed Vector assemblies and npm runtime code. It is framework-dependent, so it intentionally does not include the .NET runtime.

## Build and test

From `src/Vector.VSCode`:

```powershell
npm install
npm run build:payload
npm run compile
npm test
npm run test:extension
```

`npm test` runs Node unit tests and real process tests against the C# language server and execution host. `npm run test:extension` launches a real VS Code Extension Development Host. Set `VECTOR_VSCODE_EXECUTABLE` to a `Code.exe` path to use an existing installation; otherwise the official test runner downloads the configured stable build.

For the complete release gate, run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-VSCodeVsix.ps1
```

The gate restores/builds the solution, runs all C# tests, restores Node dependencies with `npm ci`, builds payloads, runs unit/process/Extension Host tests, creates the VSIX, audits its contents, and prints its path and SHA-256.

## Launch an Extension Development Host

Open the repository root in VS Code, select **Run Vector VS Code Extension**, and press F5. The pre-launch task builds the managed payload and TypeScript bundle. Open `examples/vscode-acceptance` in the launched window, then follow its README checklist.

## Package, audit, and install

The complete gate performs these steps. To run them separately:

```powershell
cd src\Vector.VSCode
npm run build:payload
npm run package:bundle
npx --no-install vsce package --no-dependencies --out out\vector-language-support-0.1.0.vsix
cd ..\..
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Audit-VSCodeVsix.ps1 -VsixPath .\src\Vector.VSCode\out\vector-language-support-0.1.0.vsix
code --install-extension .\src\Vector.VSCode\out\vector-language-support-0.1.0.vsix
```

After installation, open `examples/vscode-acceptance` as a folder. See its README for the complete manual test procedure.
