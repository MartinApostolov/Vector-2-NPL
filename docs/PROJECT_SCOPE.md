# Vector Project Scope

## 1. Project Objective

The objective of Vector is to design a small programming language and build a
C# interpreter capable of running programs written in that language.

The initial implementation will be a tree-walking interpreter:

```text
Vector source -> Lexer -> Parser -> AST -> Interpreter -> Result
```

This interpreter will provide a stable execution layer that could later support
additional backends or a natural-language programming interface.

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
- Approximately 8–10 example `.vec` programs
- A written grammar and language specification
- Build, run, and usage instructions

## 3. Candidate Stretch Goals

Stretch goals will be attempted only after the required interpreter is complete
and tested. They are candidates, not commitments for the MVP.

### 3.1 Built-in Functions

Useful standard functions may include:

- Console input and output
- Collection length
- Range generation
- Sum, minimum, and maximum
- Basic type inspection or conversion

### 3.2 Vector and Matrix Operations

First-class mathematical operations could give the language a more distinct
identity. Possible features include:

- Vector addition and subtraction
- Scalar multiplication
- Dot product
- Vector magnitude
- Matrices and matrix multiplication

Example syntax is not yet final, but could resemble:

```vec
let a = [1, 2, 3];
let b = [4, 5, 6];

print(a + b);
print(dot(a, b));
print(a * 2);
```

### 3.3 Bytecode Compiler and Virtual Machine

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
- A bytecode disassembler for debugging
- Compatibility tests comparing VM results with the tree-walking interpreter

The tree-walking interpreter would remain the reference implementation while
the VM is developed.

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
let values = [2, 4, 6];
print(sum(values));
```

Any such prototype should make the generated Vector representation visible
before execution so that its interpretation can be inspected and verified.

This natural-language layer is not part of the initial committed scope unless
the project requirements are clarified or expanded.

## 5. Initial Non-Goals

The first version does not aim to provide:

- Direct execution of unrestricted natural-language instructions
- A general-purpose AI or embedding model
- Native machine-code generation
- A production-scale standard library
- An optimizing compiler
- A full integrated development environment
- Compatibility with every .NET platform or operating system

These boundaries keep the required interpreter achievable and testable.

## 6. Scope Priority

Work will be prioritized in this order:

1. A correct end-to-end interpreter
2. Complete required language features
3. Tests, examples, documentation, and diagnostics
4. Vector-focused built-ins and mathematical operations
5. Bytecode VM or experimental natural-language translation

The project will be considered successful when the required interpreter is
complete and documented, even if the optional extensions are not implemented.
