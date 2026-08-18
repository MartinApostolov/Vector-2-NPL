# Vector Bytecode Compiler and Virtual Machine

**Status:** Bytecode Compiler and Virtual Machine v1 complete  
**Reference semantics:** Tree-walking interpreter  
**Bytecode format:** In-memory, stack-based, implementation-internal

Vector has two execution backends for the same language:

```text
Vector source -> Lexer -> Parser -> AST -> Tree-walking interpreter -> Result
Vector source -> Lexer -> Parser -> AST -> Bytecode compiler -> Vector VM -> Result
```

The interpreter remains the default/reference implementation. The VM is an alternative
execution backend, not a separate dialect of Vector. Backend selection is made by the
host or CLI and does not add syntax to the language.

This document describes the bytecode/VM implementation. Formal Vector syntax and
semantics remain in [LANGUAGE_SPEC.md](LANGUAGE_SPEC.md).

## 1. Pipeline

The VM path reuses the same lexer, parser, AST, runtime value model, module system,
built-ins, native modules, plugin boundary, diagnostics, and host abstractions as the
interpreter.

The high-level pipeline is:

```text
UTF-8 Vector source
    |
    v
Lexer / Parser
    |
    v
AST
    |
    v
BytecodeCompiler
    |
    v
BytecodeProgram
    |
    v
VectorVirtualMachine
    |
    v
VectorValue result / structured diagnostic
```

Local `.vec` modules loaded while the VM is running follow the same process. Native
standard-library modules and external plugin modules remain native callables and are
invoked through the same callable boundary used by the interpreter.

## 2. Instruction and chunk model

Bytecode v1 uses an internal `OpCode : byte` instruction set. A compiled entry program
contains one entry `BytecodeChunk`; compiled functions are represented by nested
`BytecodeFunctionPrototype` values whose bodies are additional chunks.

A chunk owns:

- an instruction stream;
- a constant pool;
- a name pool;
- a module-id pool;
- a function-prototype pool;
- optional source name;
- optional source text.

Instruction operands are controlled integer indexes or jump targets. Examples include
constant-pool indexes, name-pool indexes, function-prototype indexes, module-pool
indexes, list element counts, argument counts, and patched instruction targets.

The current opcode families are:

```text
values:
  Constant, Nothing, Pop

unary/binary:
  Negate, Not
  Add, Subtract, Multiply, Divide, Remainder
  Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual

scope/variables:
  EnterScope, ExitScope
  DeclareVariable, GetVariable, AssignVariable

lists/iteration:
  BuildList, RequireList, RequireBoolean
  GetIndex, SetIndex
  SnapshotList, ListCount

control flow:
  Jump, JumpIfFalse, JumpIfTrue

functions:
  MakeClosure, ValidateCall, Call, Return

modules:
  Import, GetQualifiedMember

termination:
  Halt
```

The opcode model is intentionally internal. Public embedding code uses
`VectorVmEngine`, `VectorVmSession`, `ExecutionResult`, and the human-readable
disassembly exposed by `VmCompilationResult` rather than depending on opcode ABI details.

## 3. Operand stack

The VM uses one operand/value stack for expression evaluation and call arguments/results.

Conceptually:

```text
Constant 2
Constant 3
Add
```

behaves as:

```text
[]        -> push 2 -> [2]
[2]       -> push 3 -> [2, 3]
[2, 3]    -> add    -> [5]
```

Compiled expression statements leave their value on the stack long enough for normal
top-level/final-expression behavior. Statement sequencing discards intermediate values
with `Pop` where required.

Lists are built by evaluating elements left-to-right, placing their values on the stack,
and then executing `BuildList` with the number of elements.

## 4. Instruction pointer and dispatch loop

Each active VM call frame has its own instruction pointer. The VM fetches the instruction
at that position, advances the pointer, and dispatches on its opcode.

Normal instructions continue to the next instruction. Jump instructions replace the
current frame's instruction pointer with a validated target. Function calls push another
frame; returns pop the current frame and resume the caller.

The entry frame terminates through `Halt`.

This means the VM executes compiled instructions directly rather than recursively walking
AST expression nodes.

## 5. Lexical environments

Vector v1 VM scope semantics reuse the existing runtime `Environment` model.

`EnterScope` creates a child lexical environment whose parent is the current environment.
`ExitScope` restores the parent. Variable instructions use the same environment rules as
the interpreter for:

- declaration;
- lookup;
- assignment;
- duplicate declarations;
- shadowing;
- assignment to the nearest enclosing binding;
- undefined-name diagnostics.

This is a deliberate correctness-first v1 design. Local variables are not yet compiled to
indexed stack slots.

## 6. Control-flow jumps

`if`, short-circuit boolean expressions, loops, `break`, and `continue` compile to explicit
jump instructions.

The compiler uses jump placeholders while emitting code and patches them once destination
instruction indexes are known.

Examples:

- `if` uses a conditional jump around the selected branch plus an unconditional jump
  around the alternative branch;
- `and` uses `JumpIfFalse` so the right operand is not evaluated when the left operand
  determines the result;
- `or` uses `JumpIfTrue` for the corresponding short-circuit behavior;
- `while` jumps back to its condition;
- `break` and `continue` become patched jumps;
- nested lexical scopes are explicitly unwound before loop-control jumps when necessary.

Conditions still require actual boolean values. The VM uses the same runtime validation
and diagnostic semantics as the interpreter.

## 7. Call frames

Vector functions execute with explicit VM call frames.

A frame records:

- the function's bytecode chunk;
- its current lexical environment;
- its instruction pointer;
- its operand-stack base;
- the source span of the call when applicable.

Before arguments are evaluated, `ValidateCall` checks that the callee is callable and that
the requested argument count matches its arity. This preserves Vector's interpreter
evaluation rule that a wrong-arity call fails after evaluating the callee but before
performing argument side effects.

`Call` either:

1. creates a new VM frame for a compiled Vector function; or
2. invokes an existing native/builtin callable through the VM callable bridge.

`Return` restores the caller frame and leaves the returned Vector value for the caller.

## 8. Closure model

A compiled function declaration produces a function prototype. `MakeClosure` creates a
runtime `BytecodeFunctionValue` containing:

- the prototype; and
- the lexical environment that was active when the function value was created.

Because closures capture the existing environment objects, nested functions can:

- read outer bindings;
- mutate captured bindings;
- escape their declaring function;
- share captured state;
- recurse;
- participate in mutual recursion.

The same environment-backed closure model also supports persistent functions and closures
in `VectorVmSession`.

An indexed upvalue representation may be a future optimization, but changing storage
strategy must not change Vector-language closure semantics.

## 9. Lists and numeric-list vector behavior

The VM reuses `VectorValue` and shared `RuntimeOperations`; it does not introduce VM-only
list or vector value types.

List behavior includes:

- empty and nested lists;
- zero-based indexing;
- chained indexing;
- indexed mutation;
- target/index validation;
- out-of-range diagnostics;
- cyclic-list protection.

Numeric lists continue to participate in the language's existing vector operators:

```text
numeric list + numeric list
numeric list - numeric list
numeric list * scalar
scalar * numeric list
```

Length/type checks and evaluation order are shared with the interpreter. The native
`lib.vector` and `lib.matrix` modules are also available to VM programs through the normal
module mechanism.

## 10. Source, native, and plugin modules

The VM uses the existing `ModuleLoader` and qualified module model.

### Local Vector source modules

When a VM-backed program imports a local `.vec` module, `BytecodeSourceModuleExecutor`
compiles that module and executes it with the VM while reusing the module's persistent
environment and the same program `ModuleLoader`.

This preserves:

- one-time module initialization;
- module caching/identity;
- qualified access;
- source-to-source imports;
- source-to-native imports;
- source-to-plugin imports;
- circular-import detection;
- module conflicts;
- source attribution for imported failures.

### Standard native modules

The normal standard registry remains available:

```text
lib.math
lib.collections
lib.io
lib.vector
lib.matrix
```

They are not reimplemented in bytecode. Their functions remain native Vector callables.

### External plugins

Explicitly loaded C# plugins register native Vector modules through the same public plugin
system. VM programs import and call those modules with normal Vector syntax.

Plugin selection remains a host/CLI concern:

```powershell
vector --engine vm --plugin ExamplePlugin.dll program.vec
```

The VM does not scan for plugins and does not expose arbitrary .NET methods.

## 11. Built-ins and native calls

Global built-ins are created from the same registry used by the interpreter:

```text
print
length
concat
text
number
type
range
```

Normal lexical lookup happens first, so a user binding can shadow a builtin according to
the existing language rules. If no lexical binding is found, VM lookup falls back to the
builtin registry.

Builtins and native/plugin functions implement the existing callable interface. The VM
callable bridge supplies:

- the current host;
- the current lexical environment;
- the active module loader;
- normal arity validation;
- safe translation of builtin/native runtime failures into Vector `RuntimeError`s.

This keeps standard-library and plugin behavior shared between both execution backends.

## 12. Diagnostics and source spans

Compiled instructions retain `SourceSpan` metadata. Chunks also retain source name/text
when available.

Runtime failures therefore preserve the same useful information expected from the
interpreter:

- diagnostic code;
- message;
- source span;
- source file/module name;
- source text where available.

The high-level `VectorVmEngine` and `VectorVmSession` translate VM/runtime/module failures
into the repository's normal `ExecutionResult` / `Diagnostic` model.

Interpreter/VM compatibility tests cover successful behavior and failure/source
information across language features, modules, native calls, and plugins.

## 13. Disassembler

`BytecodeDisassembler` produces stable human-readable output for debugging and tests.

The public high-level compile API exposes that text:

```csharp
var engine = new VectorVmEngine();
var compilation = engine.Compile("1 + 2;");
Console.WriteLine(compilation.Disassembly);
```

The CLI also exposes VM disassembly:

```powershell
vector --engine vm --disassemble program.vec
```

The disassembly command parses and compiles the file, prints the bytecode, and does not
execute program side effects.

The textual format is intended for inspection and regression testing. It is not a
serialized bytecode file format or a public binary compatibility contract.

## 14. Interpreter compatibility strategy

The tree-walking interpreter is the semantic reference implementation.

The VM was built to reuse backend-independent components rather than duplicate language
rules. Shared pieces include:

- `VectorValue` types;
- `RuntimeOperations`;
- builtin creation;
- lexical environment behavior;
- module resolution/loading;
- native-module registry;
- native callable interfaces;
- external plugin registration/loading;
- host input/output abstractions;
- structured diagnostic types.

Compatibility coverage executes representative/current language behavior through both
`VectorEngine` and `VectorVmEngine` and compares:

- final values;
- output;
- module behavior;
- plugin behavior;
- diagnostic code/source information.

The automated coverage includes expressions, scopes, lists/vector operations, control
flow, loops, functions/recursion/closures, builtins, standard modules, local modules,
plugins, matrix/vector libraries, examples, deterministic I/O, and failure cases.

A difference in internal instructions or disassembly is allowed. A difference in Vector
language semantics is treated as a compatibility bug unless deliberately documented.

## 15. Public VM execution API

For reusable/in-process execution, `VectorVmEngine` is the supported high-level VM entry
point:

```csharp
using Vector.Core;

var engine = new VectorVmEngine();

var compilation = engine.Compile("1 + 2;", "sample.vec");
if (compilation.Success)
{
    Console.WriteLine(compilation.Disassembly);
}

var result = engine.Execute("print(21 * 2);");
```

`Execute` captures output in the returned `ExecutionResult` and may also forward output to
a supplied `IVectorHost`. A program root may be supplied for resolving local source
modules.

For incremental/interactive hosts, create a persistent session:

```csharp
var session = engine.CreateSession();

session.Execute("let value = 10;");
var result = session.Execute("value + 5;");
```

The session compiles each submission independently but reuses its environment/module
state.

`VectorPluginRuntime` also exposes VM execution while retaining its existing interpreter
execution API, allowing plugin-aware embedding hosts to choose a backend explicitly.

## 16. CLI and REPL usage

The default remains the interpreter:

```powershell
vector program.vec
vector
```

Explicit backend selection:

```powershell
vector --engine interpreter program.vec
vector --engine vm program.vec
vector --engine vm
```

VM plus trusted plugin:

```powershell
vector --engine vm --plugin ExamplePlugin.dll program.vec
```

Compile/disassemble without execution:

```powershell
vector --engine vm --disassemble program.vec
```

`--engine` accepts `interpreter` or `vm`. `--disassemble` requires the VM backend and a
source file.

## 17. Deliberate v1 non-goals

The completed VM v1 deliberately does **not** include:

- a serialized `.vbc` bytecode file format;
- a stable public binary/opcode ABI;
- bytecode loading from arbitrary files;
- an optimizing compiler;
- indexed local-variable slots;
- indexed closure upvalues;
- JIT compilation;
- native machine-code generation;
- a bytecode debugger/stepper or breakpoints;
- VM-specific Vector syntax;
- a separate VM module/plugin system;
- automatic plugin discovery;
- sandboxing of external C# plugins;
- Visual Studio editor integration.

Environment-backed locals/closures are an intentional v1 correctness choice. A later
implementation may optimize locals and captures to indexed slots/upvalues while preserving
the same observable Vector behavior.

The next planned major stretch goal is the **Visual Studio Community Extension**. That
phase should have its own implementation plan and is not part of Bytecode/VM v1.
