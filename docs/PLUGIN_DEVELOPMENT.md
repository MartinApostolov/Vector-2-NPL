# Vector External C# Plugin Development

**Status:** Controlled External C# Plugin Support v1  
**Plugin API version:** `VectorPluginApi.CurrentVersion == 1`  
**Current target framework:** .NET 8 (`net8.0`)

This guide describes the external C# plugin model implemented by Vector. The working reference
project is [`examples/plugins/Vector.ExamplePlugin`](../examples/plugins/Vector.ExamplePlugin/README.md).

## Trust and security boundary

> Loading a Vector C# plugin executes trusted .NET code in the Vector process. Only load plugin
> assemblies you trust.

Plugins are **not sandboxed**. A plugin constructor, registration method, native function, or any
other plugin code executes in the Vector process with the same operating-system permissions as that
process.

Vector therefore deliberately does **not**:

- scan arbitrary directories and auto-load DLLs;
- load every DLL beside a `.vec` program;
- expose arbitrary .NET classes/methods to Vector by reflection;
- provide source-language DLL-loading syntax;
- treat arbitrary NuGet packages as automatically callable Vector libraries.

The loader uses reflection only to locate the defined Vector plugin entry contract inside a DLL
that the CLI or embedding host explicitly selected.

## Architecture overview

External plugins reuse Vector's existing native-module model:

```text
explicit DLL path
    -> Vector plugin loader
    -> one public IVectorPlugin entry point
    -> API/version + identity validation
    -> transactional module registration
    -> NativeModuleRegistry
    -> normal Vector import/member/call rules
```

A plugin does not create a second language import system. If C# registers `example.tools`, Vector
code uses it exactly like another qualified native module:

```vec
import example.tools;
print(example.tools.double(21));
```

The standard library remains present in the default plugin runtime. Source modules, standard native
modules, and plugin modules all occupy the same qualified module namespace and use the same conflict
rules.

## Create a plugin project

The v1 host targets **.NET 8**, so plugin projects should target `net8.0`.

Inside this repository, the copyable example references both `Vector.Plugins` (the plugin contract)
and `Vector.Core` (module definitions, native functions, runtime values/converters):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="path\to\Vector.Core\Vector.Core.csproj" />
    <ProjectReference Include="path\to\Vector.Plugins\Vector.Plugins.csproj" />
  </ItemGroup>
</Project>
```

There is no Vector package manager or published automatic plugin package format in v1. The
repository example uses project references so the contract and module APIs are explicit.

## Implement `IVectorPlugin`

A selected plugin assembly must contain exactly one supported public plugin entry type. The entry
must be a concrete public class implementing `IVectorPlugin` with a public parameterless
constructor.

Minimal shape:

```csharp
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Plugins;

public sealed class ExamplePlugin : IVectorPlugin
{
    public string Id => "example.tools.plugin";

    public int ApiVersion => VectorPluginApi.CurrentVersion;

    public void Register(IVectorPluginContext context)
    {
        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "example", "tools" }),
                module =>
                {
                    module.Export("answer", NativeValueConverter.FromNumber(42));

                    module.Export(
                        "double",
                        new NativeFunction(
                            "double",
                            1,
                            (_, arguments) =>
                            {
                                var value = NativeValueConverter.ToNumber(arguments[0], "value");
                                return NativeValueConverter.FromNumber(value * 2);
                            }));
                }));
    }
}
```

### Plugin id

`Id` is the stable identity used by the registration manager. In v1 it must be non-empty and is
compared using ordinal string equality. Loading/registering the same id twice in one manager is an
error.

Use a stable, project-specific id rather than generating a different value on every load.

### API version

Return:

```csharp
public int ApiVersion => VectorPluginApi.CurrentVersion;
```

The current value is `1`. The host rejects a plugin whose declared API version does not exactly
match the host's supported version. This fails before any plugin modules are committed.

## Register qualified Vector modules

`Register(IVectorPluginContext context)` stages one or more `NativeModuleDefinition` instances:

```csharp
context.RegisterModule(
    new NativeModuleDefinition(
        new ModuleId(new[] { "my", "math" }),
        module =>
        {
            // explicit exports
        }));
```

That module is used from Vector as:

```vec
import my.math;
```

A plugin may register several distinct module ids. Registration is transactional: modules are
staged first and committed only after plugin registration succeeds and conflicts are checked.

The host rejects:

- the same module id registered twice by one plugin;
- a plugin module that conflicts with an already registered standard/native module;
- a later plugin module that conflicts with a module from an earlier plugin.

A failed registration does not leave the failed plugin's earlier staged modules installed.

## Export values and native functions

A module initializer explicitly decides what Vector can see. Public C# methods are not exported
automatically.

Export a value:

```csharp
module.Export("answer", NativeValueConverter.FromNumber(42));
```

Export a function:

```csharp
module.Export(
    "double",
    new NativeFunction(
        "double",
        1,
        (_, arguments) =>
        {
            var value = NativeValueConverter.ToNumber(arguments[0], "value");
            return NativeValueConverter.FromNumber(value * 2);
        }));
```

`NativeFunction` uses Vector's normal callable boundary. Its declared arity is enforced by the
runtime. Plugin functions participate in the same deterministic Vector evaluation/call behavior as
built-in native functions.

## Convert values explicitly

Use `NativeValueConverter` rather than relying on arbitrary object reflection or implicit
coercion. The example plugin demonstrates controlled number/text conversion:

```csharp
var number = NativeValueConverter.ToNumber(arguments[0], "value");
return NativeValueConverter.FromNumber(number * 2);
```

```csharp
var name = NativeValueConverter.ToText(arguments[0], "name");
return NativeValueConverter.FromText($"Hello, {name}!");
```

Conversion/type failures become structured Vector runtime failures through the native-call
boundary. Plugins should return valid Vector runtime values and should not return `null`.

## Build the DLL

For the repository example:

```powershell
dotnet build examples/plugins/Vector.ExamplePlugin/Vector.ExamplePlugin.csproj
```

Debug output:

```text
examples/plugins/Vector.ExamplePlugin/bin/Debug/net8.0/Vector.ExamplePlugin.dll
```

For your own project, use its normal `dotnet build` output. The DLL passed to Vector must be a
compatible managed `.dll` containing the single supported `IVectorPlugin` entry point.

## Deploy managed dependencies

A plugin may use private managed helper assemblies. Keep those dependency DLLs beside the plugin
assembly in the deployed plugin output. Preserve any normal build metadata/output needed by those
assemblies as appropriate.

Vector resolves plugin-private managed dependencies relative to the selected plugin. Core host
assemblies such as `Vector.Core` and `Vector.Plugins` are shared with the host so the plugin and
runtime use the same Vector contract/type identity.

If a required private managed dependency is missing or incompatible, plugin loading fails before
registration with a structured load error. Vector v1 does not download or restore missing plugin
dependencies.

## Load a plugin from the CLI

File execution:

```powershell
vector --plugin ExamplePlugin.dll program.vec
```

Using the repository project directly:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- --plugin .\examples\plugins\Vector.ExamplePlugin\bin\Debug\net8.0\Vector.ExamplePlugin.dll .\examples\15_external_plugin\main.vec
```

Multiple explicit plugins:

```powershell
vector --plugin PluginA.dll --plugin PluginB.dll program.vec
```

`--plugin` requires one following DLL path and may repeat. At most one `.vec` entry file may be
supplied. Plugins load before program execution.

Plugin load/registration failures are CLI setup failures (exit code `2`) and are reported with
concise messages rather than normal raw stack traces.

## Load a plugin for the REPL

Start the REPL with a plugin:

```powershell
vector --plugin ExamplePlugin.dll
```

Then import its module normally:

```vec
import example.tools;
example.tools.double(21);
```

The loaded plugin remains registered for the REPL lifetime. Loading the DLL does not implicitly
import its modules and does not create unqualified member names.

## Load plugins from an embedding host

`VectorPluginRuntime` provides the supported convenience path for a host that wants the default
standard library plus explicit external plugins:

```csharp
using Vector.Plugins;

var runtime = VectorPluginRuntime.CreateDefault(
    @"C:\plugins\PluginA.dll",
    @"C:\plugins\PluginB.dll");

var result = runtime.Execute(source, programRoot);
```

The returned runtime exposes the shared `NativeModules`, `Plugins`, and `Engine` instances. Modules
remain registered for that runtime/manager lifetime. A host can also call
`runtime.Plugins.LoadFromPath(path)` before later executions when it deliberately wants to add
another trusted plugin.

## Use the module from Vector source

Plugin implementation language does not change Vector syntax:

```vec
import example.tools;

print(example.tools.answer);
print(example.tools.double(21));
print(example.tools.greet("Vector"));
```

Expected repository example output:

```text
42
42
Hello, Vector!
```

Normal module rules still apply. Using `example.tools` without importing it fails normally, and a
local `.vec` module with the same qualified id creates an explicit source/native module conflict.

## Compatibility, conflicts, and errors

### Assembly/load failures

`VectorPluginLoadException` reports controlled load categories through
`VectorPluginLoadErrorKind`, including invalid/missing paths, wrong extension, malformed or
incompatible assemblies/dependencies, entry-point shape/count problems, and constructor failures.

### Registration failures

`VectorPluginException` reports controlled registration categories through `VectorPluginErrorKind`:

- invalid plugin id;
- API-version mismatch;
- duplicate plugin id;
- duplicate module within a plugin;
- module conflict;
- registration failure.

Registration is transactional with respect to the plugin's modules.

### Plugin-function runtime failures

Once registered, plugin native functions execute through Vector's existing native-call boundary.
A plugin may deliberately report a `NativeRuntimeException`/Vector diagnostic. Unexpected C#
exceptions and invalid/null returns are converted to safe Vector diagnostics rather than exposing
raw implementation stack traces to Vector code.

## Unsupported in plugin v1

The following are intentionally outside Controlled External C# Plugin Support v1:

- sandboxing untrusted .NET code;
- automatic plugin-directory discovery or scanning;
- loading DLLs from Vector source code;
- unrestricted reflection over arbitrary .NET APIs;
- automatically exposing public C# methods/classes;
- automatic NuGet/package exposure or dependency restoration;
- a Vector package manager/manifest for external plugins;
- plugin hot reload or unloading (plugin load contexts are process/runtime-lifetime in v1);
- version ranges or side-by-side plugin API negotiation beyond exact `CurrentVersion` matching.

The next major planned project phase is the **Bytecode Compiler and Virtual Machine**. External
plugin support should remain behind the same native module/callable boundary so the future VM can
reuse it rather than create a separate interop system.
