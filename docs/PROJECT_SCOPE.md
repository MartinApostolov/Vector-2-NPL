# Vector Project Scope

**Status:** Required interpreter MVP complete; native-library foundation and Standard Library + Linear Algebra v1 stretch phases complete; later extensions remain planned below.

## 1. Project Objective

The objective of Vector is to design a small programming language and build a
C# interpreter capable of running programs written in that language.

The initial implementation is a tree-walking interpreter:

```text
Vector source -> Lexer -> Parser -> AST -> Interpreter -> Result
```

This interpreter provides the stable reference execution layer for later
language libraries, additional execution backends, editor tooling, and a
possible natural-language programming interface.

## 2. Required Deliverables

### 2.1 Language Design

Vector will have a documented, unambiguous syntax and grammar covering:

- Variable declarations and assignment
- Numbers, strings, booleans, and vectors
- Arithmetic, comparison, and logical expressions
- Operator precedence
- Conditional statements using `if` and `else`
- Loops
- Functions, parameters, and return values
- Local and global scopes

The exact syntax will be defined in a separate language specification as the
design is finalized.

### 2.2 Lexer

The lexer will read Vector source text and produce a stream of tokens. It will:

- Recognize keywords, identifiers, literals, operators, and punctuation
- Track source positions and line numbers
- Report invalid characters and malformed literals clearly

### 2.3 Parser and Abstract Syntax Tree

The parser will validate the token stream and construct an abstract syntax tree
(AST). It will:

- Parse statements and expressions according to the language grammar
- Apply the correct operator precedence and associativity
- Produce useful syntax errors with source locations
- Represent programs with explicit expression and statement nodes

### 2.4 Tree-Walking Interpreter

The interpreter will execute the AST directly. It will support:

- Evaluation of literals and expressions
- Variable definition, lookup, and assignment
- Branching and iteration
- Function declarations and calls
- Return values
- Nested local scopes and global scope
- Runtime type checks and understandable runtime errors

### 2.5 Execution Interfaces

Users will be able to run Vector in two ways:

- Execute a `.vec` source file from the command line
- Enter and execute code interactively through a REPL

### 2.6 Tests, Examples, and Documentation

The repository will include:

- Lexer and parser tests
- Interpreter and runtime tests
- Tests for invalid syntax and runtime failures
- A focused set of example `.vec` programs (currently 14 entry-point examples)
- A written grammar and language specification
- Build, run, and usage instructions

The required interpreter MVP described in Section 2 is complete. The
tree-walking interpreter remains the reference implementation for later
extensions.

## 3. Candidate Stretch Goals and Post-MVP Extensions

Stretch goals are attempted only after the required interpreter is complete and
tested. They are extensions to the finished MVP, not requirements for the
academy deliverable.

Some foundations for these goals were implemented during the MVP, including
local multi-file modules, several core built-ins, and basic numeric-list vector
operations.

### 3.1 Vector Library System and C#/.NET-backed Modules

Vector already supports local multi-file programs using qualified module paths:

```vec
import lib.geometry;
lib.geometry.distance(a, b);
```

A local module such as `lib.geometry` can currently be implemented by a Vector
source file such as:

```text
lib/geometry.vec
```

This library-system foundation is now implemented: the same qualified Vector module
model can be backed by either local Vector source or explicitly registered C#/.NET
code.

Vector code uses a library through the same public Vector API regardless of whether
the implementation is written in Vector or C#:

```vec
import lib.math;

let root = lib.math.sqrt(25);
```

A C#-backed Vector module may:

- expose functions and values through an explicit Vector module interface;
- convert between supported Vector runtime values and controlled C# types;
- call appropriate .NET APIs such as `System.Math` internally;
- use third-party .NET libraries internally when they provide useful,
  well-tested functionality;
- report failures through Vector's structured diagnostics rather than leaking
  raw host exceptions;
- remain usable by the tree-walking interpreter and later execution backends.

The initial native-library bridge uses explicit registration and a deliberate
public API. Arbitrary reflection over any installed .NET assembly, unrestricted
access to all .NET APIs, and automatic loading of arbitrary DLLs are not part of
this completed foundation.

Vector-source modules and C#-backed modules coexist behind the same qualified
module concept. Conflicting registrations or ambiguous module identities are
handled explicitly rather than silently choosing one.

This foundation is complete and remains the shared library/callable boundary that
later execution backends and external plugin support should reuse.

### 3.2 Built-in Functions and Standard-Library Foundations

The planned built-in/standard-library foundation for this phase is complete.

Global built-ins now include:

- `print`
- `length`
- `range`
- `concat`
- `text`
- `number`
- `type`

Broader functionality is kept behind qualified standard-library modules rather than
flattened into global built-ins. The default runtime now includes:

```text
lib.math
lib.collections
lib.io
lib.vector
lib.matrix
```

This phase added collection-wide `sum`/`min`/`max`, host-backed line input through
`lib.io.readLine()`, and basic runtime type inspection through `type(value)`.
Additional collection helpers may still be added later if they prove useful, but a
large general-purpose standard library is not required for this project.

### 3.3 Vector and Matrix Operations

First-class mathematical operations can give the language a more distinct
identity.

The MVP already supports numeric-list:

- Vector addition
- Vector subtraction
- Scalar multiplication in both directions

For example:

```vec
let a = [1, 2, 3];
let b = [4, 5, 6];

print(a + b);
print(a * 2);
```

The planned vector/matrix stretch functionality for this phase is complete through
qualified C#-backed standard modules:

```text
lib.vector.dot(a, b)
lib.vector.magnitude(v)
lib.vector.normalize(v)

lib.matrix.shape(matrix)
lib.matrix.transpose(matrix)
lib.matrix.add(a, b)
lib.matrix.multiply(a, b)
```

Vectors remain ordinary numeric lists. Matrices are non-empty rectangular nested
numeric lists with non-empty rows. Neither introduces a separate runtime type, and
matrix-shaped lists do not overload the core `+` or `*` operators.

More advanced linear-algebra functionality may still be added later if useful, but it
is not part of the completed Standard Library + Linear Algebra v1 phase.

### 3.4 External C# Library and Plugin Support

This is the **next major planned stretch goal**. Vector's own C#-backed module
interface is now proven by the built-in standard modules, so the next step may open
that controlled mechanism to separately compiled external libraries.

A future plugin/library SDK could allow a developer to write a C# assembly that:

- references a small public Vector library interface;
- registers one or more qualified Vector modules;
- exposes approved functions and values;
- performs explicit Vector/C# value conversion;
- returns Vector-compatible errors and diagnostics.

This stage should build on the same mechanism used by Vector's own native
standard-library modules rather than creating a second interop system.

Directly exposing arbitrary methods from arbitrary DLLs through reflection is
not the initial goal. A controlled registration model is preferred because it
keeps overload resolution, type conversion, diagnostics, compatibility, and
security understandable.

### 3.5 Bytecode Compiler and Virtual Machine

An alternative execution backend could compile the AST into custom Vector
bytecode and run it on a stack-based virtual machine:

```text
Vector source -> Lexer -> Parser -> AST -> Bytecode compiler -> Vector VM -> Result
```

This extension could include:

- A Vector instruction set and constant table
- A bytecode compiler
- A value stack and instruction pointer
- Global and local variable instructions
- Conditional and unconditional jumps
- Function call frames
- Calls into the same built-in and library interface used by the interpreter
- A bytecode disassembler for debugging
- Compatibility tests comparing VM results with the tree-walking interpreter

The tree-walking interpreter will remain the reference implementation while the
VM is developed.

### 3.6 Visual Studio Community Extension

A Visual Studio Community extension could make Vector available as an
installable VSIX package and provide editor integration for `.vec` files.

The initial extension could include:

- Vector file recognition and syntax highlighting
- Automatic indentation, bracket matching, and comment support
- Commands to run or check the current Vector file
- Syntax and semantic diagnostics displayed in the editor
- Output from the Vector runtime inside Visual Studio
- Awareness of Vector modules and library-qualified names
- Later support for completion, hover information, and navigation through a
  shared language server

The extension would reuse the existing Vector core, CLI, diagnostics, runtime,
and library metadata rather than duplicate the language implementation. This
would also allow a future natural-language layer to generate Vector code,
display it for inspection, and run it through the same tooling.

A custom Visual Studio project system, integrated debugger, and deployment
interface are outside the initial extension goal.

### 3.7 Package and Dependency Management

Package management should be considered only after external Vector/C# libraries
have a stable loading and compatibility model.

A future package system could provide:

- A small project or package manifest
- Declared Vector-library dependencies
- Version information and compatibility rules
- Predictable local dependency resolution
- Installation or restoration of approved Vector packages
- Integration with the CLI and future Visual Studio tooling

NuGet may still be used internally by the C# implementation of Vector or its
native libraries. Directly treating arbitrary NuGet packages as automatically
callable Vector libraries is not required.

A package manager should not be designed before the underlying external-library
model is proven, because the package format should describe a real and stable
library interface rather than predict one prematurely.

## 4. Experimental Future Direction: Natural-Language Programming

The project concept mentions natural language and vector embeddings. A realistic
future architecture could add an NLP or AI layer before the formal Vector
language:

```text
Natural-language instruction
-> NLP/AI translation
-> Vector source code or AST
-> Interpreter or virtual machine
-> Result
```

For example, a future system might translate:

```text
Create a vector containing 2, 4, and 6, then display its sum.
```

into formal Vector code such as:

```vec
import lib.collections;

let values = [2, 4, 6];
print(lib.collections.sum(values));
```

Generated code should use the same qualified standard-library APIs as hand-written
Vector source rather than inventing unqualified helpers that do not exist.

Any such prototype should make the generated Vector representation visible
before execution so that its interpretation can be inspected and verified.

This natural-language layer remains experimental and should be attempted after
the formal language, library model, execution backends, and core tooling are
stable enough to provide a deterministic target.

## 5. Initial Non-Goals

The first version does not aim to provide:

- Direct execution of unrestricted natural-language instructions
- A general-purpose AI or embedding model
- Native machine-code generation
- A production-scale standard library
- Unrestricted reflection-based access to arbitrary .NET APIs
- Automatic execution of arbitrary DLLs or NuGet packages as Vector code
- An optimizing compiler
- A full integrated development environment
- Compatibility with every .NET platform or operating system

These boundaries keep the required interpreter achievable and testable while
allowing controlled post-MVP growth.

## 6. Scope Priority

The required interpreter MVP is complete. Post-MVP status and remaining priority are:

1. Preserve the completed tree-walking interpreter as the reference
   implementation.
2. **Complete:** C#/.NET-backed Vector library foundation with existing `.vec`
   modules using the same qualified module model.
3. **Complete:** built-in/standard-library foundation for this phase, including
   `type`, `lib.math`, `lib.collections`, and `lib.io`.
4. **Complete:** planned vector/matrix functionality for this phase through
   `lib.vector` and `lib.matrix`.
5. **Next:** add controlled external C# library/plugin support using the proven
   native library interface.
6. Build the bytecode compiler and stack-based virtual machine against the same
   runtime values, callable contracts, and library boundary.
7. Build the Visual Studio Community extension on top of the stable language,
   diagnostics, runtime, and library metadata.
8. Add package/dependency management if the external library ecosystem makes it
   useful.
9. Prototype experimental natural-language translation last, targeting the
   formal Vector language and its established libraries.

The exact size of each stretch goal should still be reconsidered against
remaining project time and academy expectations. The project is already
successful at the required-MVP level; later work should extend it without
destabilizing the tested interpreter.
