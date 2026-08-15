# Vector-2-NPL

Vector is a small, formally defined programming language and interpreter being
developed for Sirma Academy — Project #2.

The first version focuses on the foundations of language implementation: source
code is tokenized, parsed into an abstract syntax tree (AST), and executed by a
tree-walking interpreter. Natural-language programming is a possible future
extension built on top of this foundation; it is not part of the initial MVP.

## Team

- Martin Apostolov

## Technology

- C# / .NET
- Command-line application
- Automated tests

## Planned MVP

- Lexer and parser
- Abstract syntax tree
- Numbers, strings, booleans, and vectors
- Variables and expressions
- Conditional statements and loops
- Functions with parameters
- Local and global scopes
- Tree-walking interpreter
- Clear syntax and runtime errors with line information
- REPL and `.vec` file execution
- Parser and runtime tests
- Example Vector programs

## Project Direction

The required deliverable is a conventional interpreted language with precise
syntax and deterministic behavior:

```text
Vector source -> Lexer -> Parser -> AST -> Interpreter -> Result
```

A future natural-language layer could translate human instructions into Vector
source code or directly into an AST before execution:

```text
Natural language -> NLP/AI translation -> Vector code or AST -> Interpreter -> Result
```

Potential stretch goals include first-class vector and matrix operations,
additional built-in functions, and a bytecode compiler with a stack-based
virtual machine.

See the [Project Scope](docs/PROJECT_SCOPE.md) for the committed deliverables,
boundaries, and stretch-goal priority. See the
[Vector Language Specification](docs/LANGUAGE_SPEC.md) for the formal syntax,
semantics, runtime value model, module design, and future-facing language rules.
Where the language specification describes functionality beyond the committed
MVP, the Project Scope remains authoritative for implementation priority.

## Project Status

Language design specified; implementation setup is next.

## Running the Project

Build and usage instructions will be added when the first executable version is
available.
