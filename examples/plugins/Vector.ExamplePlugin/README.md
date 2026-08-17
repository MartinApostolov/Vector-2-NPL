# Vector.ExamplePlugin

This project is a minimal external C# plugin for Vector. It is deliberately small so it can be copied as a starting point for another plugin.

It demonstrates:

- one public `IVectorPlugin` entry type;
- a stable plugin id;
- `VectorPluginApi.CurrentVersion`;
- explicit registration of the qualified module `example.tools`;
- an exported constant;
- `NativeFunction` exports;
- `NativeValueConverter` for controlled Vector/C# value conversion.

The plugin does not expose arbitrary .NET methods through reflection. Only the values and functions explicitly registered in `Register(...)` become Vector module members.

## Build

From the repository root:

```powershell
dotnet build examples/plugins/Vector.ExamplePlugin/Vector.ExamplePlugin.csproj
```

The Debug build DLL is written to:

```text
examples/plugins/Vector.ExamplePlugin/bin/Debug/net8.0/Vector.ExamplePlugin.dll
```

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

The Vector source still imports the module normally with `import example.tools;`. Loading the DLL is an explicit host/CLI action, not Vector source syntax.
