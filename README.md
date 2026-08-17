# Vector-2-NPL

Vector is a small, formally defined programming language implemented in C#/.NET.
The current implementation is a tree-walking interpreter built for Sirma Academy —
Project #2. Vector source is deliberately strict and deterministic; a possible
natural-language front end is a later direction rather than part of the v1 parser.

```text
Vector source -> Lexer -> Parser -> AST -> Interpreter -> Result
```

## What Vector currently supports

- UTF-8 `.vec` source files and Unicode identifiers
- numbers, text, booleans, lists, functions, and `nothing`
- dynamically typed variables with strict runtime operations and no implicit coercion
- arithmetic, comparison, equality, and boolean operators
- mutable lists, zero-based indexing, and indexed assignment
- numeric-list vector addition/subtraction and scalar multiplication
- lexical block scope, shadowing, and assignment to enclosing bindings
- `if` / `else if` / `else`, `while`, and `for ... in`
- `break` and `continue`
- named functions, recursion, closures, and `return`
- local multi-file modules with qualified access such as `lib.geometry.move(...)`
- explicitly registered C#/.NET-backed modules using the same qualified `import` model
- native standard-library modules: `lib.math`, `lib.collections`, `lib.io`, `lib.vector`, and `lib.matrix`
- built-ins: `print`, `length`, `concat`, `text`, `number`, `type`, and `range`
- structured lexer, parser, module, native-call, and runtime diagnostics with source locations
- `.vec` command-line execution, a reusable `VectorEngine`, and an interactive REPL
- automated tests and 14 focused example entry points/programs

The formal language rules are in [docs/LANGUAGE_SPEC.md](docs/LANGUAGE_SPEC.md).
The academy/project boundaries and future directions are in
[docs/PROJECT_SCOPE.md](docs/PROJECT_SCOPE.md).

## Prerequisites

Vector targets **.NET 8 (`net8.0`)**.

Required for command-line development:

- .NET 8 SDK
- Git, if cloning the repository from GitHub

Optional development environment:

- Visual Studio 2022 with .NET 8 development support

No external runtime service, database, package manager for Vector code, or external
native library is required. Vector's current native standard library is compiled into
the runtime and uses .NET APIs internally. NuGet restore is needed for the xUnit test
dependencies.

Check the installed SDK with:

```powershell
dotnet --version
```

## Clone and build

```powershell
git clone https://github.com/MartinApostolov/Vector-2-NPL.git
cd Vector-2-NPL
dotnet restore Vector.sln
dotnet build Vector.sln
```

In Visual Studio, open `Vector.sln` and use **Build -> Rebuild Solution**.

## Run the tests

From the repository root:

```powershell
dotnet test Vector.sln
```

After a successful build, you may avoid rebuilding during the test command:

```powershell
dotnet test Vector.sln --no-build
```

In Visual Studio, open **Test Explorer** and choose **Run All Tests**.

## Run a `.vec` file

Use the CLI project and pass exactly one `.vec` file:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- examples/01_hello.vec
```

Expected output:

```text
Hello, Vector!
```

A multi-file example can be run the same way:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- examples/10_modules/main.vec
```

The C#/.NET-backed standard-library examples are runnable through the same CLI path:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- examples/11_native_math.vec
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- examples/12_standard_library.vec
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- examples/13_vector_math.vec
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- examples/14_matrix_math.vec
```

They import standard modules with ordinary Vector module syntax; no separate
library-loading command is required.

On Windows, after building, the generated executable can also be run directly:

```powershell
.\src\Vector.Cli\bin\Debug\net8.0\Vector.Cli.exe .\examples\01_hello.vec
```

The directory containing the launched entry file becomes that program's module
root. For example, an entry file at `examples/10_modules/main.vec` can import
`lib.geometry`, which resolves to `examples/10_modules/lib/geometry.vec`.

### CLI exit codes

| Exit code | Meaning |
| ---: | --- |
| `0` | Program completed successfully |
| `1` | Vector lexical, syntax, module, name/scope, or runtime failure |
| `2` | CLI/file problem such as bad arguments, wrong extension, or unreadable input |

The file runner accepts UTF-8 `.vec` source. Diagnostics include the source file,
line, column, diagnostic code, relevant source line, and a marker when source
information is available.

## Use the REPL

Launch the CLI without a source-file argument:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj
```

Example session:

```text
Vector REPL. Type :exit or :quit to leave.
vector> let value = 10;
vector> value;
10
vector> function double(number) {
...>     return number * 2;
...> }
vector> double(6);
12
vector> :exit
```

The REPL:

- preserves successful top-level variables and functions between submissions;
- displays the value of a final expression statement when it is not `nothing`;
- keeps running after a syntax or runtime error;
- uses `...> ` while braces, parentheses, or brackets remain open;
- resolves modules relative to the directory from which the REPL was launched;
- exits with `:exit`, `:quit`, or end-of-input.

## Language quick tour

### Variables and strict types

Variables are dynamically typed, but operations do not coerce values implicitly:

```vec
let value = "20";
print(value);

value = 20;
print(value + 5);
```

Output:

```text
20
25
```

This is a runtime error rather than an automatic conversion:

```vec
let value = "20";
value + 5;
```

Use `number(value)` or `text(value)` when an explicit conversion is wanted.
Use `type(value)` to inspect the current runtime type:

```vec
print(type(20));        // number
print(type("hello"));   // text
print(type([1, 2, 3])); // list
```

`type` returns one of `number`, `text`, `boolean`, `list`, `function`, or `nothing`.
Numeric lists, vectors, and matrix-shaped nested lists are still runtime type `list`.

### Conditions and loops

```vec
let total = 0;

for number in range(1, 5) {
    if number > 2 {
        total = total + number;
    }
}

print(total);
```

`if` and `while` conditions must be actual booleans; Vector has no truthiness.

### Functions and closures

```vec
let counter = 0;

function increase() {
    counter = counter + 1;
    return counter;
}

print(increase());
print(increase());
```

Functions capture their lexical declaration environment and can read or assign
bindings in enclosing scopes.

### Lists and numeric-list vector operations

```vec
let left = [1, 2, 3];
let right = [4, 5, 6];

print(left + right);  // [5, 7, 9]
print(left * 2);      // [2, 4, 6]
```

Lists are mutable and zero-indexed:

```vec
let values = [10, 20, 30];
values[1] = 50;
print(values[1]);
```

Only lists whose current elements are all numbers participate in numeric-list
vector operations. General list concatenation uses `concat(a, b)`.

Additional vector mathematics is provided through `lib.vector`:

```vec
import lib.vector;

print(lib.vector.dot([1, 2, 3], [4, 5, 6])); // 32
print(lib.vector.magnitude([3, 4]));          // 5
print(lib.vector.normalize([3, 4]));          // [0.6, 0.8]
```

Vectors remain ordinary numeric lists; `lib.vector` does not introduce a separate
runtime vector type.

### Modules

`examples/10_modules/main.vec` demonstrates local modules:

```vec
import lib.geometry;

let moved = lib.geometry.move([1, 2], [3, 4]);
print(moved);
```

The module file is `examples/10_modules/lib/geometry.vec`. Imported members stay
behind the full qualified module path; they are not flattened into the caller's
ordinary variable scope.

The same Vector-facing syntax can also refer to an explicitly registered native
C#/.NET-backed module:

```vec
import lib.math;

print(lib.math.sqrt(25));
print(lib.math.pi);
```

Source modules and native modules share the same qualified module identity and access
rules. A native module is available only when the host runtime registers it. If a
local `.vec` file and a native registration both claim the same qualified module name,
Vector reports an explicit module conflict instead of silently choosing one.

Native modules are registered deliberately by the host; Vector does not scan arbitrary
assemblies, reflect over installed .NET APIs, or load arbitrary DLLs as modules.

## Native standard library

The default runtime registers the standard modules described below. They use the same
qualified import/member syntax as local `.vec` modules, and importing a standard module
does not flatten its members into global names.

### `lib.math`

`lib.math` is registered by the default `VectorEngine`, CLI, and REPL runtime:

| Member | Behavior |
| --- | --- |
| `lib.math.pi` | .NET `System.Math.PI` as a Vector number |
| `lib.math.e` | .NET `System.Math.E` as a Vector number |
| `lib.math.abs(value)` | Absolute value |
| `lib.math.sqrt(value)` | Square root |
| `lib.math.min(a, b)` | Smaller number |
| `lib.math.max(a, b)` | Larger number |
| `lib.math.pow(base, exponent)` | Exponentiation |

All function arguments above must be Vector numbers. Arity is strict. Native numeric
results must be finite; `NaN` and infinities are rejected as structured Vector runtime
errors. Importing `lib.math` does not create unqualified names such as `sqrt`, `max`,
or `pi`.

### `lib.collections`

```vec
import lib.collections;

let values = [4, -2, 8, 3];

print(lib.collections.sum(values)); // 13
print(lib.collections.min(values)); // -2
print(lib.collections.max(values)); // 8
```

These functions require a list containing only finite numbers and do not mutate the
input list. `sum([])` returns `0`; `min([])` and `max([])` are invalid operations and
produce structured Vector runtime errors. These aggregate `min`/`max` functions are
separate from the two-argument scalar functions `lib.math.min(a, b)` and
`lib.math.max(a, b)`.

### `lib.io`

```vec
import lib.io;

let line = lib.io.readLine();
```

`lib.io.readLine()` takes no arguments and reads one line from the configured host
input stream. It preserves ordinary leading/trailing spaces and returns `nothing` at
end-of-input. The CLI and REPL provide input-capable hosts. Embedders that call
`VectorEngine` must provide an input-capable host to use `readLine`; otherwise the call
fails with a structured Vector runtime diagnostic.

### `lib.vector`

```text
lib.vector.dot(a, b)
lib.vector.magnitude(v)
lib.vector.normalize(v)
```

All arguments are ordinary finite numeric lists. `dot` requires equal lengths.
`magnitude([])` is `0`. `normalize` returns a new list and rejects any zero-magnitude
vector, including `[]`. Vectors remain runtime type `list`.

### `lib.matrix`

Matrices are represented as ordinary nested numeric lists:

```vec
let matrix = [
    [1, 2],
    [3, 4]
];
```

A valid matrix is non-empty, every row is a non-empty list, all rows have the same
length, and every cell is a finite number. The module provides:

```text
lib.matrix.shape(matrix)
lib.matrix.transpose(matrix)
lib.matrix.add(a, b)
lib.matrix.multiply(a, b)
```

`shape` returns `[rows, columns]`. `transpose` returns a new matrix. `add` requires
equal shapes. `multiply(a, b)` uses ordinary row-by-column matrix multiplication and
requires the number of columns in `a` to equal the number of rows in `b`.

Matrices remain nested `list` values. Vector does not define matrix `+` or `*`
operators in this version; use the qualified `lib.matrix` functions instead.

## Core built-ins

| Built-in | Behavior |
| --- | --- |
| `print(value)` | Writes one formatted value and a newline; text nested inside lists retains double quotes |
| `length(value)` | List element count or Unicode-scalar count for text |
| `concat(a, b)` | Returns a new shallow list containing the elements of two lists |
| `text(value)` | Explicitly converts a Vector value to its display text |
| `number(value)` | Accepts a number or finite numeric text and returns a number |
| `type(value)` | Returns the current runtime type name as text |
| `range(start, end)` | Ascending whole-number list from `start` inclusive to `end` exclusive |

`range(start, end)` returns `[]` when `start >= end`. Both bounds must be finite
whole numbers. Built-in names can be shadowed by ordinary declarations.

## Example programs

The `examples/` directory provides a focused tour:

1. [`01_hello.vec`](examples/01_hello.vec) — output
2. [`02_variables.vec`](examples/02_variables.vec) — dynamic variable values
3. [`03_conditions.vec`](examples/03_conditions.vec) — branching
4. [`04_while_loop.vec`](examples/04_while_loop.vec) — `while`
5. [`05_for_loop.vec`](examples/05_for_loop.vec) — `for` and `range`
6. [`06_functions.vec`](examples/06_functions.vec) — functions and recursion
7. [`07_lists.vec`](examples/07_lists.vec) — lists and collection built-ins
8. [`08_vectors.vec`](examples/08_vectors.vec) — numeric-list vector operations
9. [`09_scopes.vec`](examples/09_scopes.vec) — lexical scope and outer assignment
10. [`10_modules/main.vec`](examples/10_modules/main.vec) — multiple local files
11. [`11_native_math.vec`](examples/11_native_math.vec) — C#/.NET-backed `lib.math`
12. [`12_standard_library.vec`](examples/12_standard_library.vec) — `type` and `lib.collections`
13. [`13_vector_math.vec`](examples/13_vector_math.vec) — `lib.vector`
14. [`14_matrix_math.vec`](examples/14_matrix_math.vec) — `lib.matrix`

All example programs are also covered by automated execution tests.

## Diagnostics

Vector keeps diagnostics structured inside `Vector.Core`. The CLI presents errors
in a source-oriented form such as:

```text
path\program.vec:2:1: error RuntimeTypeError: ...
    value + "bad";
    ^^^^^^^^^^^^^
```

Errors originating in imported source modules retain the imported module's
file/source identity rather than being incorrectly attributed to the entry file.
Native call failures use the Vector call-site span and are converted to structured
Vector diagnostics; unexpected host exceptions do not expose raw C# exception details
or stack traces to Vector code.

## Project structure

```text
src/Vector.Core/   language front end, runtime, modules, public execution API
src/Vector.Cli/    file runner, diagnostic formatting, REPL
tests/Vector.Tests automated lexer/parser/runtime/integration/example tests
examples/          runnable Vector programs
docs/              project scope and formal language specification
```

## Future work

The required tree-walking interpreter remains the reference implementation. The
post-MVP native-library foundation and the planned Standard Library + Linear Algebra v1
phase are implemented: source and registered native modules share one qualified module
model, with `lib.math`, `lib.collections`, `lib.io`, `lib.vector`, and `lib.matrix`
available in the default runtime.

The next major planned stretch goal is controlled external C# library/plugin support
built on the proven native-module boundary. Later goals remain the custom bytecode
compiler and VM, a Visual Studio Community extension, package/dependency management if
useful, and eventually an inspectable natural-language translation layer. Arbitrary DLL
loading and automatic reflection over .NET APIs are not implemented.
