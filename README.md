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
- built-ins: `print`, `length`, `concat`, `text`, `number`, and `range`
- structured lexer, parser, module, and runtime diagnostics with source locations
- `.vec` command-line execution and an interactive REPL
- automated tests and 10 focused example programs

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

No external runtime service, database, package manager for Vector code, or native
library is required. NuGet restore is needed for the xUnit test dependencies.

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

## Core built-ins

| Built-in | Behavior |
| --- | --- |
| `print(value)` | Writes one formatted value and a newline; text nested inside lists retains double quotes |
| `length(value)` | List element count or Unicode-scalar count for text |
| `concat(a, b)` | Returns a new shallow list containing the elements of two lists |
| `text(value)` | Explicitly converts a Vector value to its display text |
| `number(value)` | Accepts a number or finite numeric text and returns a number |
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

All example programs are also covered by automated execution tests.

## Diagnostics

Vector keeps diagnostics structured inside `Vector.Core`. The CLI presents errors
in a source-oriented form such as:

```text
path\program.vec:2:1: error RuntimeTypeError: ...
    value + "bad";
    ^^^^^^^^^^^^^
```

Errors originating in imported modules retain the imported module's file/source
identity rather than being incorrectly attributed to the entry file.

## Project structure

```text
src/Vector.Core/   language front end, runtime, modules, public execution API
src/Vector.Cli/    file runner, diagnostic formatting, REPL
tests/Vector.Tests automated lexer/parser/runtime/integration/example tests
examples/          runnable Vector programs
docs/              project scope and formal language specification
```

## Future work

The required interpreter remains the priority and reference implementation.
Candidate post-MVP directions include more built-ins/vector mathematics, a custom
bytecode compiler and VM, a Visual Studio Community extension, and eventually an
inspectable natural-language translation layer. These are future directions, not
part of Vector v1's required interpreter behavior.
