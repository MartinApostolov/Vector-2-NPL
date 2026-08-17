# Vector.ExamplePlugin

This project is the repository's minimal, copyable external C# plugin example for Vector.
For the complete authoring/deployment model, see
[`docs/PLUGIN_DEVELOPMENT.md`](../../../docs/PLUGIN_DEVELOPMENT.md).

> Loading a Vector C# plugin executes trusted .NET code in the Vector process. Only load
> plugin assemblies you trust.

The example demonstrates:

- one public `IVectorPlugin` entry type;
- the stable plugin id `example.tools.plugin`;
- `VectorPluginApi.CurrentVersion`;
- explicit registration of the qualified module `example.tools`;
- an exported constant (`answer`);
- `NativeFunction` exports (`double` and `greet`);
- `NativeValueConverter` for controlled Vector/C# value conversion;
- ordinary Vector `import example.tools;` usage.

The plugin does not expose arbitrary .NET methods through reflection. Only the values and
functions explicitly registered in `Register(...)` become Vector module members.

## Build

The v1 host and this example target **.NET 8 (`net8.0`)**. From the repository root:

```powershell
dotnet build examples/plugins/Vector.ExamplePlugin/Vector.ExamplePlugin.csproj
```

The Debug build DLL is written to:

```text
examples/plugins/Vector.ExamplePlugin/bin/Debug/net8.0/Vector.ExamplePlugin.dll
```

If a real plugin has private managed dependencies, deploy the dependency DLLs beside the plugin
assembly (and keep the normal build output required by those dependencies together). Vector does
not fetch or restore plugin dependencies at runtime.

## Run the Vector example

After building the plugin:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- --plugin .\examples\plugins\Vector.ExamplePlugin\bin\Debug\net8.0\Vector.ExamplePlugin.dll .\examples\15_external_plugin\main.vec
```

Expected output:

```text
42
42
Hello, Vector!
```

The Vector source still imports the module normally with `import example.tools;`. Loading the DLL
is an explicit host/CLI action, not Vector source syntax.

## Try it in the REPL

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- --plugin .\examples\plugins\Vector.ExamplePlugin\bin\Debug\net8.0\Vector.ExamplePlugin.dll
```

Then:

```vec
import example.tools;
example.tools.answer;
example.tools.double(21);
example.tools.greet("Vector");
```

Plugin modules remain qualified modules; loading a DLL does not create unqualified globals.
