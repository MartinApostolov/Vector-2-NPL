# Vector Architecture

**Status:** Submission architecture overview  
**Runtime:** C# / .NET 8  
**Execution backends:** Tree-walking interpreter and stack-based bytecode VM

Vector is a small formally defined programming language. The repository is organized so
that the language front end and most runtime semantics are shared, while execution can
continue through either the original interpreter or the bytecode compiler/VM.

```text
                    +----------------+
Vector source ----->| Lexer / Parser |
                    +--------+-------+
                             |
                             v
                            AST
                          /     \
                         v       v
                 Interpreter   Bytecode Compiler
                         |       |
                         |       v
                         |    Vector VM
                         \       /
                          v     v
                      Shared Runtime
                            |
                 Modules / Native Calls
                            |
         Standard Library / External Plugins
```

The tree-walking interpreter remains the default and semantic reference implementation.
The VM is a second execution backend for the same language rather than a separate Vector
dialect.

## 1. Source pipeline, lexer, parser, and AST

Vector source is read as UTF-8 text and represented with source-position information.
The `Lexer` converts characters into tokens for identifiers, keywords, literals,
operators, and punctuation while retaining spans used by diagnostics. Lexer failures are
reported as Vector diagnostics rather than host exceptions.

The `Parser` consumes those tokens, applies the language grammar and operator precedence,
and produces a `CompilationUnit` AST. Statements and expressions are represented by
explicit syntax-node classes, including declarations, blocks, control flow, functions,
imports, calls, lists, indexing, assignments, and qualified module member access.

Both execution backends consume this same AST. Syntax and grammar therefore do not change
when the user chooses the interpreter or VM.

## 2. Tree-walking interpreter

`VectorEngine` is the high-level interpreter API. It parses a source submission, creates
a module loader for the program root, and executes the AST through `Interpreter`.

The interpreter evaluates expression nodes and executes statement nodes directly. Blocks
create child lexical environments, functions capture the environment in which they are
declared, and control-flow operations such as `return`, `break`, and `continue` are kept
inside the Vector runtime rather than delegated to generated host code.

This backend was implemented first and remains the reference for Vector-language
semantics. Newer VM behavior is checked against it rather than defining a second set of
rules.

## 3. Bytecode compiler and stack-based VM

The VM path begins after parsing. `BytecodeCompiler` compiles the shared AST into an
in-memory `BytecodeProgram`. Entry code and function bodies use bytecode chunks containing
an instruction stream plus controlled pools for constants, names, module ids, and function
prototypes. Instructions retain source-span metadata for diagnostics and disassembly.

`VectorVirtualMachine` executes the compiled program with:

- an operand/value stack for expression values and call arguments/results;
- an explicit instruction pointer for each active chunk;
- explicit call frames for compiled Vector functions;
- jumps for conditionals, short-circuit boolean operators, loops, `break`, and `continue`;
- compiled closures that capture the same lexical environment model used by the
  interpreter.

The VM deliberately reuses backend-independent runtime behavior instead of reimplementing
Vector semantics. The repository includes compatibility tests that compare interpreter and
VM results, output, failures, modules, plugins, examples, and source diagnostics.

Detailed VM design is documented separately in [BYTECODE_VM.md](BYTECODE_VM.md).

## 4. Shared runtime, values, and lexical scopes

Both backends use the same runtime value model. `VectorValue` has the language-level kinds
`number`, `text`, `boolean`, `list`, `function`, and `nothing`. Numeric vectors are ordinary
numeric lists, and matrices are rectangular nested numeric lists; neither introduces a
separate runtime type.

Shared runtime helpers implement the core operation and validation rules used across the
language. This keeps arithmetic, comparisons, boolean requirements, list/index behavior,
vector-style numeric-list operators, callable validation, and error behavior aligned
between execution backends.

Lexical scope is represented by `Environment`. Each environment stores bindings for one
scope and optionally points to an enclosing environment. Declaration is local to the
current scope; lookup walks outward; assignment updates the nearest existing binding.
This provides block scope, shadowing, closures, recursion, and mutation of captured
bindings. The v1 VM reuses these environment objects for correctness rather than compiling
locals and closure upvalues to indexed slots.

## 5. Module system

Vector modules use qualified identities such as `lib.geometry` or `lib.math`.
`ModuleResolver` maps local module identities to `.vec` files under the program root, and
`ModuleLoader` owns loading, caching, initialization, and conflict handling.

The loader provides the same module semantics to both backends:

- qualified imports and qualified member access;
- one-time module initialization and module caching;
- local source-to-source imports;
- source-to-native and source-to-plugin imports;
- circular-import detection;
- explicit conflicts when a local source module and native module claim the same id;
- source identity retained for failures raised inside imported files.

For VM execution, `BytecodeSourceModuleExecutor` compiles imported `.vec` modules and
executes them with the VM while preserving the same `ModuleLoader` and module environments.

## 6. Standard library and native-call boundary

C#/.NET-backed modules do not bypass Vector's module rules. They are explicitly registered
in `NativeModuleRegistry` and occupy the same qualified namespace as source modules.
Vector's default runtime registers:

```text
lib.math
lib.collections
lib.io
lib.vector
lib.matrix
```

Native functions cross the runtime boundary through explicit Vector callable/value
conversion code. This gives native modules access to appropriate .NET implementation
facilities while keeping Vector-facing types, arity checks, failures, and diagnostics
controlled by the language runtime. The interpreter and VM call the same registered
native functions through their shared callable boundary.

## 7. External C# plugin boundary

External plugins extend the same native-module system rather than adding a second import
mechanism. `Vector.Plugins` provides the public `IVectorPlugin` / `IVectorPluginContext`
contract, DLL loading, API-version validation, identity checks, and transactional module
registration.

Plugin loading is explicit: the CLI or embedding host supplies a DLL path. A selected
assembly must expose the supported plugin entry contract and explicitly register each
Vector-facing module/member. Vector does not auto-scan directories and does not expose
arbitrary .NET methods to Vector by reflection.

Plugins are trusted in-process .NET extensions and are **not sandboxed**. Loading a plugin
runs its code with the permissions of the Vector process, so only trusted plugin DLLs
should be selected. See [PLUGIN_DEVELOPMENT.md](PLUGIN_DEVELOPMENT.md) for the full plugin
contract and deployment model.

## 8. CLI, REPL, and embedding APIs

`Vector.Cli` provides the evaluator-facing executable path. With a `.vec` file it reads the
source, chooses an execution backend, runs the program, and formats diagnostics. With no
source file it starts the REPL.

Important CLI configuration includes:

```text
--engine interpreter|vm
--disassemble
--plugin <path-to-dll>
```

The interpreter is the default. `--disassemble` is VM-only and compiles/prints deterministic
bytecode without executing program side effects. Repeated `--plugin` options explicitly
load trusted extensions before file execution or REPL startup.

Embedding hosts can use `VectorEngine` for interpreter execution, `VectorVmEngine` for VM
compilation/execution, and `VectorVmSession` for persistent VM submissions. `VectorPluginRuntime`
creates interpreter and VM engines that share one standard/native/plugin registry. Host
interfaces abstract output and optional input so the same language runtime can be used by
the CLI, REPL, tests, or another .NET host.

## 9. Diagnostics and source locations

Source positions and spans are carried from the front end into syntax nodes and runtime
failures. Diagnostics use structured codes, severity, messages, spans, and source identity
rather than relying on raw exception text.

Parser errors are returned before execution. Interpreter runtime errors retain their
source span. VM instructions also retain source spans and chunks retain source name/text,
allowing VM failures to map back to Vector source. Imported module failures keep the
imported file's source identity, and the CLI formats available file, line, column, source
line, and marker information for the user.

## 10. Key design decisions

- **One language, two backends.** The interpreter stays the semantic reference and the VM
  targets the same AST and observable behavior.
- **Share semantics rather than duplicate them.** Runtime values, operations, environments,
  modules, native calls, host interfaces, and diagnostics are reused across backends.
- **Qualified modules stay qualified.** Imports such as `lib.geometry` do not flatten
  exported names into the caller's ordinary variable scope.
- **Lists are the mathematical data foundation.** Vectors and matrices build on ordinary
  lists instead of adding new language-level runtime types.
- **Native access is explicit.** Standard modules and plugins expose deliberate Vector APIs;
  arbitrary .NET reflection is not a language feature.
- **Correctness before VM optimization.** Bytecode is in-memory and the VM reuses lexical
  environment objects before considering slot/upvalue optimizations.
- **Natural language remains outside the formal core.** A future NLP layer may translate
  instructions into inspectable Vector source/AST, but the current lexer/parser remain
  deterministic.

## 11. Deliberate non-goals

The current submission deliberately does not include:

- direct execution of unrestricted natural-language instructions;
- a general-purpose AI or embedding model;
- a Visual Studio Community extension;
- Vector package/dependency management;
- automatic loading of arbitrary DLLs or NuGet packages;
- a plugin security sandbox;
- a serialized `.vbc` bytecode format or stable opcode ABI;
- an optimizing/JIT or native machine-code compiler;
- indexed local/upvalue VM optimization;
- a production-scale standard library, full IDE, or integrated debugger.

These boundaries keep the completed interpreter, libraries/plugins, and VM testable and
stable for the academy submission while leaving clear directions for future work.
