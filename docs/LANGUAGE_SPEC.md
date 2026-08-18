# Vector Language Specification

**Status:** Vector v1 language semantics with interpreter and VM execution backends  
**Project:** Vector-2-NPL  
**File extension:** `.vec`  
**Reference implementation:** C# / .NET 8 tree-walking interpreter

This document defines the formal Vector v1 language implemented by the repository.
The core language is deterministic and strict. Future natural-language tooling may
translate human instructions into Vector source or the same semantic structures,
but natural-language interpretation is not part of the v1 parser.

## 1. Design principles

Vector v1 follows these rules:

1. Variables are dynamically typed; values have runtime types.
2. Operations are strict about the runtime values they accept.
3. Vector performs no implicit type coercion.
4. Conditions require actual booleans; there is no truthiness.
5. Evaluation order is defined and deterministic.
6. Blocks and functions use lexical scope.
7. Lists are ordinary mutable lists; a list whose current contents are all numbers
   can additionally participate in numeric-list vector operations.
8. Modules are local `.vec` files or explicitly registered native C#/.NET modules,
   both accessed through their full qualified paths.
9. Diagnostics preserve structured source information, including the originating
   imported module when an error occurs there.
10. The tree-walking interpreter is the v1 reference implementation.
11. The bytecode VM targets the same language semantics; choosing a backend is a
    host/CLI execution setting, not Vector source syntax.

The two execution pipelines are:

```text
Vector source -> Lexer -> Parser -> AST -> Tree-walking interpreter -> Result
Vector source -> Lexer -> Parser -> AST -> Bytecode compiler -> Vector VM -> Result
```

The grammar and semantic rules in this document do not change based on the selected
backend. Bytecode opcodes are an implementation detail and are documented separately
in `docs/BYTECODE_VM.md`, not in the formal grammar.

## 2. Source files and encoding

Vector source files use the `.vec` extension.

The command-line runner reads source as UTF-8 and rejects invalid UTF-8 input.
Unicode text and Unicode identifiers are supported.

Whitespace separates tokens but is otherwise insignificant. Newlines do not end
statements. Statements that require terminators use `;`.

```vec
let name = "Vector";
let count = 3;
print(name);
```

## 3. Lexical rules

### 3.1 Identifiers

Identifier rules are Unicode-aware:

- the first character is `_` or a Unicode letter;
- later characters may be `_`, Unicode letters, Unicode combining marks, or
  Unicode decimal digits;
- identifier comparison is case-sensitive;
- identifiers are normalized to Unicode NFC before name comparison.

Examples:

```vec
let playerHealth = 100;
let здраве = 100;
let число2 = 5;
let _temporary = 3;
```

These are different identifiers:

```text
player
Player
PLAYER
```

Keywords use exact lowercase spelling.

### 3.2 Keywords

Vector v1 reserves:

```text
let
if
else
while
for
in
function
return
break
continue
true
false
nothing
and
or
not
import
```

### 3.3 Numbers

Vector exposes one numeric runtime type: `number`.

Valid numeric literal forms include integers, decimals, and scientific notation:

```vec
0
20
3.14
1000.5
1e3
2.5e-4
```

A leading `-` is unary negation, not part of the literal.

A decimal point requires digits on both sides:

```text
0.5   valid
5.0   valid
.5    invalid
5.    invalid
```

Numeric separators are not part of v1.

The implementation uses `double` internally. Source numeric literals must parse to
finite values. Ordinary arithmetic exposes the language-level `number` type.

Division is mathematical rather than integer division:

```vec
5 / 2;  // 2.5
```

Using zero as the right operand of `/` or `%` is a runtime error.

### 3.4 Text

Text literals use double quotes:

```vec
"hello"
"Здравей"
```

Single-quoted strings are not part of v1. Multiline text literals are not supported.

Supported escapes are:

```text
\"   double quote
\\   backslash
\n   newline
\r   carriage return
\t   tab
```

Unknown escape sequences produce a lexical diagnostic.

### 3.5 Boolean and nothing literals

```vec
true
false
nothing
```

`nothing` is a real runtime value with its own type. It is not false and does not
participate in truthiness.

### 3.6 Comments

Line comments start with `//`:

```vec
// one line
let value = 10;
```

Block comments use `/* ... */`:

```vec
/* multiple
   lines */
let value = 10;
```

Block comments do not nest. An unterminated block comment is a lexical error.

## 4. Runtime value model

Vector v1 runtime values are:

```text
number
text
boolean
list
function
nothing
```

A variable's type is the type of its current value. A binding may later hold a value
of a different type:

```vec
let value = "20";
value = 20;
```

That is valid. Operations still inspect the current values strictly:

```vec
let value = "20";
value + 5;  // runtime type error
```

Modules are namespaces, not ordinary first-class variable values.

### 4.1 No implicit coercion

Vector never silently changes unrelated runtime types:

```vec
5 + "2";       // error
"Age: " + 20; // error
```

Explicit conversions are available through `text(value)` and `number(value)`.

### 4.2 Equality

`==` and `!=` do not coerce types.

```vec
5 == 5;           // true
5 == "5";         // false
"abc" == "abc"; // true
```

Rules:

- numbers compare by numeric value;
- text compares by text value;
- booleans compare by boolean value;
- `nothing == nothing` is true;
- values of different runtime types are unequal;
- lists compare recursively by length and element equality;
- functions compare by identity, not by declaration text or behavior.

## 5. Lists and numeric-list vector behavior

### 5.1 Lists

Lists are ordered, zero-indexed, mutable values:

```vec
let values = [10, 20, 30];
let names = ["Ada", "Bob"];
let mixed = [1, "hello", true, nothing];
let nested = [[1, 2], [3, 4]];
```

Indexing:

```vec
values[0]; // 10
values[1]; // 20
```

An index must be a non-negative whole number inside the list bounds. These are
runtime errors:

```vec
values[-1];
values[1.5];
values[10];
```

Indexed assignment replaces an element and evaluates to the assigned value:

```vec
values[1] = 50;
```

Vector v1 forbids cyclic list structures. An assignment that would make a list
directly or indirectly contain itself fails at runtime.

### 5.2 Numeric lists

A list remains a `list`; Vector has no separate permanent vector runtime type.

A list whose **current** elements are all numbers is a numeric list. Numeric-list
eligibility is recomputed from current contents. The empty list counts as a numeric
list of length zero.

```vec
let values = [1, 2, 3];
values * 2; // [2, 4, 6]

values[1] = "two";
values * 2; // runtime type error

values[1] = 2;
values * 2; // valid again
```

### 5.3 Vector addition

Two numeric lists of equal length may be added element-by-element:

```vec
[1, 2] + [3, 4]; // [4, 6]
```

Different lengths are a runtime error.

### 5.4 Vector subtraction

Two numeric lists of equal length may be subtracted element-by-element:

```vec
[1, 2] - [3, 4]; // [-2, -2]
```

Different lengths are a runtime error.

### 5.5 Scalar multiplication

A numeric list and a number may be multiplied in either order:

```vec
[1, 2, 3] * 2;
2 * [1, 2, 3];
```

Both produce:

```vec
[2, 4, 6]
```

List/list multiplication, dot product, magnitude, and matrix arithmetic are not v1
operators. Dot product, magnitude, normalization, and matrix operations are available
through the qualified `lib.vector` and `lib.matrix` standard modules defined below.

### 5.6 General list concatenation

`+` is not general list concatenation. Numeric lists use `+` for vector addition;
non-numeric lists cannot use `+`.

Use the built-in:

```vec
concat([1, 2], [3, 4]); // [1, 2, 3, 4]
```

`concat` creates a new **shallow** list. It does not mutate either source list or
deep-copy nested values.

## 6. Variables and assignment

### 6.1 Declaration

Variables are declared with `let` and require an initializer:

```vec
let x = 10;
let name = "Vector";
let enabled = true;
```

The initializer is evaluated before the new binding is introduced.

### 6.2 Assignment

Assignment changes an existing binding:

```vec
x = 20;
```

Assignment to an undeclared ordinary name is an error:

```vec
missing = 20; // error
```

Assignment expressions evaluate to the assigned value. Assignment is
right-associative.

### 6.3 Redeclaration

The same name cannot be declared twice in the same lexical scope:

```vec
let x = 10;
let x = 20; // error
```

### 6.4 Shadowing

A nested scope may declare a new binding with the same name:

```vec
let x = 10;

{
    let x = 20;
    print(x); // 20
}

print(x); // 10
```

### 6.5 Assignment to enclosing bindings

Assignment resolves the nearest existing lexical binding:

```vec
let counter = 0;

function increase() {
    counter = counter + 1;
}
```

Calling `increase()` changes the outer `counter`.

## 7. Expressions and operators

### 7.1 Arithmetic

Numeric operators are:

```text
+  -  *  /  %
```

Unary numeric negation uses `-`.

`+` additionally supports:

- text + text -> text concatenation;
- numeric list + numeric list -> vector addition.

`-` additionally supports numeric-list subtraction, and `*` additionally supports
numeric-list scalar multiplication as defined above.

No other implicit overload or coercion is performed.

### 7.2 Comparison

Ordering operators are:

```text
<  <=  >  >=
```

They are defined for numbers. Ordering unrelated runtime types is a runtime error.

### 7.3 Logical operators

```text
and  or  not
```

These accept booleans only. There is no truthiness.

`and` and `or` short-circuit:

```vec
false and dangerousCall(); // right side is not evaluated
true or dangerousCall();   // right side is not evaluated
```

### 7.4 Evaluation order

Except where short-circuiting skips an operand, expression operands are evaluated
left-to-right.

For function calls:

1. the callee is evaluated;
2. argument count is checked;
3. arguments are evaluated left-to-right;
4. the function is called.

A wrong argument count therefore fails before argument expressions run.

### 7.5 Operator precedence

From highest to lowest:

| Precedence | Operators/forms |
| ---: | --- |
| 1 | grouping `(...)`, calls `(...)`, indexing `[...]`, qualified access `.` |
| 2 | unary `not`, unary `-` |
| 3 | `*`, `/`, `%` |
| 4 | `+`, `-` |
| 5 | `<`, `<=`, `>`, `>=` |
| 6 | `==`, `!=` |
| 7 | `and` |
| 8 | `or` |
| 9 | assignment `=` |

Assignment associates right-to-left. Other binary operators above associate
left-to-right.

## 8. Blocks and lexical scope

Blocks use braces and create lexical scopes:

```vec
{
    let local = 10;
    print(local);
}

print(local); // error
```

Function calls create function-local scopes whose parent is the environment captured
when the function was declared.

## 9. Conditional statements

Conditions do not require parentheses:

```vec
if score >= 90 {
    print("A");
} else if score >= 80 {
    print("B");
} else {
    print("C");
}
```

Parentheses may be used for ordinary grouping.

Every `if` condition must evaluate to a boolean. Only the selected branch executes.

## 10. Loops

### 10.1 While

```vec
while x < 10 {
    x = x + 1;
}
```

The condition is checked before each iteration and must be boolean.

### 10.2 For-in

`for` iterates over a list:

```vec
for item in items {
    print(item);
}
```

Semantics:

- the iterable expression is evaluated once when the loop starts;
- it must produce a list;
- iteration uses a shallow snapshot of the list elements captured at loop start;
- structural changes to the original list do not change the set of values visited;
- the loop variable is local to the loop;
- each iteration uses a fresh iteration scope.

Numeric iteration uses the implemented `range` built-in:

```vec
for number in range(1, 4) {
    print(number);
}
```

Output:

```text
1
2
3
```

### 10.3 Break and continue

`break` exits the nearest enclosing loop. `continue` skips to the next iteration of
the nearest enclosing loop.

Using either outside a loop is a syntax/context error.

## 11. Functions

### 11.1 Declaration and calls

Functions are named declarations:

```vec
function add(a, b) {
    return a + b;
}

let result = add(5, 3);
```

Parameters are dynamically typed local bindings. Argument count is strict.

```vec
function add(a, b) {
    return a + b;
}

add(1);       // error
add(1, 2, 3); // error
```

Duplicate parameter names are invalid. Vector v1 has no parameter or return type
annotations.

### 11.2 Return

```vec
function square(x) {
    return x * x;
}
```

A bare `return;` returns `nothing`. Reaching the end of a function without executing
`return` also returns `nothing`.

Using `return` outside a function is invalid.

### 11.3 Functions as values

Functions are runtime values:

```vec
function add(a, b) {
    return a + b;
}

let operation = add;
print(operation(2, 3));
```

Function equality is identity-based.

### 11.4 Closures and recursion

A function captures the lexical environment in which its declaration executes.
That captured environment remains available if the function escapes its original
scope.

Function declarations are not hoisted: the binding becomes available when the
declaration executes. The function's own binding is available to its body, enabling
recursion.

```vec
function factorial(value) {
    if value <= 1 {
        return 1;
    }

    return value * factorial(value - 1);
}
```

## 12. Top-level execution

Vector does not require a `main()` function. A source file may contain executable
top-level code, which runs in source order:

```vec
let x = 5;
print(x);
```

## 13. Modules and multiple files

A Vector module has a qualified `ModuleId` and a persistent module environment.
A module implementation may be either:

- a local `.vec` source file; or
- an explicitly registered native C#/.NET module supplied by the host runtime.

Both implementations use the same Vector-facing import and qualified-member syntax.
A program may import a local source module:

```vec
import lib.geometry;
```

If the entry file is:

```text
MyProgram/main.vec
```

then `import lib.geometry;` resolves to:

```text
MyProgram/lib/geometry.vec
```

The entry file's directory is the program root for file execution.

### 13.1 Qualified access

Imported members are accessed through the module's full path:

```vec
import lib.geometry;
lib.geometry.distance(a, b);
```

Vector does not automatically shorten that to `geometry.distance(...)`.

Different full paths remain distinct:

```vec
import game.geometry;
import math.geometry;

game.geometry.distance(a, b);
math.geometry.distance(a, b);
```

### 13.2 Module scope

Each module has its own isolated top-level environment.

Every top-level declaration in an imported module is externally accessible through
that module's full qualified path. Vector v1 has no `export` keyword.

A module member does not become an unqualified variable in the importing module.
Block-local and function-local bindings are never module members.

Imports are not automatically re-exported: a module can use modules it directly
imports, while its importer must import a dependency itself if it wants direct
qualified access to that dependency.

### 13.3 Import placement

Imports are top-level declarations and must appear before other top-level
declarations or executable statements within a source file/submission.

Imports are not allowed inside functions, loops, conditionals, or blocks.

### 13.4 Module initialization and caching

A source module's top-level code executes when it is first imported. A native module's
registered initializer populates its persistent module environment when it is first
loaded. Each module is initialized at most once per program execution/module-loader
lifetime, even when multiple imports reach it.

Dependencies initialize as their importing module executes its import statements. The
REPL retains one module loader for the session, so an imported module's initialization
and cache identity also persist across successful REPL submissions.

### 13.5 Circular imports

Circular dependencies are errors. Diagnostics identify the dependency cycle when
possible.

### 13.6 Module namespace

Qualified module paths occupy a namespace separate from ordinary variable bindings.
General object/member access is not part of Vector v1.

### 13.7 Native modules and source/native conflicts

Native modules are registered explicitly by the host runtime using the same qualified
module ids that source imports use. They do not require a fake source path, source
text, or parsed AST.

Resolution for one qualified module id is:

```text
registered native only  -> load native module
existing .vec file only -> load source module
neither                  -> ModuleNotFound
both                     -> ModuleConflict
```

Vector never silently prefers a native module over a source module, or vice versa.

A native initializer exports named Vector runtime values into that module's persistent
environment. Native callable values participate in ordinary Vector call syntax and
strict arity checking. Supported host-value conversion is explicit and controlled;
there is no general reflection-based conversion of arbitrary C# objects.

Native modules are not automatically discovered by scanning assemblies. A host may
explicitly load a supported external Vector plugin DLL, but Vector source itself has no
DLL-loading statement or builtin. Loading a plugin does not expose arbitrary .NET methods:
only values and functions deliberately registered into qualified Vector modules are visible.
Vector does not provide unrestricted .NET reflection/API access, a package manifest, or a
package manager.

### 13.8 Standard native module: `lib.math`

The default runtime used by `VectorEngine`, the CLI, and the REPL registers:

```vec
import lib.math;
```

Its public members are:

```text
lib.math.pi
lib.math.e
lib.math.abs(value)
lib.math.sqrt(value)
lib.math.min(a, b)
lib.math.max(a, b)
lib.math.pow(base, exponent)
```

`pi` and `e` are module values. They do not become unqualified globals.

Every function above accepts only Vector numbers and uses strict fixed arity. The
implementation uses .NET `System.Math` where appropriate. Native numeric results must
be finite; `NaN`, positive infinity, and negative infinity are rejected as structured
Vector runtime failures.

Example:

```vec
import lib.math;

print(lib.math.sqrt(25));
print(lib.math.max(3, 7));
print(lib.math.pi);
```

### 13.9 Standard native module: `lib.collections`

The default runtime registers:

```vec
import lib.collections;
```

Its public functions are:

```text
lib.collections.sum(values)
lib.collections.min(values)
lib.collections.max(values)
```

All three functions require exactly one list argument whose elements are finite
Vector numbers. They do not mutate the input list.

- `sum([])` returns `0`;
- `min([])` is invalid and produces a structured runtime failure;
- `max([])` is invalid and produces a structured runtime failure.

The collection-wide `min` and `max` functions are separate from the two-argument
scalar `lib.math.min(a, b)` and `lib.math.max(a, b)` functions.

Example:

```vec
import lib.collections;

let values = [4, -2, 8, 3];
print(lib.collections.sum(values)); // 13
print(lib.collections.min(values)); // -2
print(lib.collections.max(values)); // 8
```

### 13.10 Standard native module: `lib.io`

The default runtime registers:

```vec
import lib.io;
```

Its public function is:

```text
lib.io.readLine()
```

`readLine` has arity zero. It reads one line from the input capability supplied by
the current Vector host:

- an available line is returned as Vector `text`;
- ordinary leading and trailing spaces are preserved;
- end-of-input returns `nothing`;
- calling `readLine` without an input-capable host is a structured runtime failure.

The repository CLI and REPL use input-capable hosts. Embedding applications using
`VectorEngine` must provide an input-capable host when they want `lib.io.readLine()`.

### 13.11 Standard native module: `lib.vector`

The default runtime registers:

```vec
import lib.vector;
```

Its public functions are:

```text
lib.vector.dot(a, b)
lib.vector.magnitude(v)
lib.vector.normalize(v)
```

A vector argument is an ordinary Vector list whose current elements are all finite
numbers. The module does not introduce a separate vector runtime type; `type(v)` still
returns `list`.

`dot(a, b)`:

- requires numeric lists of equal length;
- returns the sum of element-wise products;
- returns `0` for two empty vectors;
- rejects mismatched lengths and non-finite intermediate results.

`magnitude(v)`:

- returns the Euclidean magnitude `sqrt(sum(v[i] * v[i]))`;
- returns `0` for `[]`;
- rejects non-finite intermediate/results.

`normalize(v)`:

- returns a new numeric list with each element divided by the vector magnitude;
- does not mutate the input list;
- rejects every zero-magnitude vector, including `[]`.

Example:

```vec
import lib.vector;

print(lib.vector.dot([1, 2, 3], [4, 5, 6])); // 32
print(lib.vector.magnitude([3, 4]));          // 5
print(lib.vector.normalize([3, 4]));          // [0.6, 0.8]
```

### 13.12 Standard native module: `lib.matrix`

The default runtime registers:

```vec
import lib.matrix;
```

A matrix is represented by an ordinary nested Vector list. A valid matrix must be:

- a non-empty outer list;
- made only of non-empty row lists;
- rectangular, so every row has the same number of columns;
- made only of finite numeric cells.

Examples:

```vec
[[1, 2], [3, 4]] // valid 2x2 matrix
[[1], [2], [3]]  // valid 3x1 matrix
[]               // invalid
[[1, 2], [3]]    // invalid: ragged
```

Matrices remain ordinary runtime `list` values. There is no distinct matrix runtime
type in this version.

The module provides:

```text
lib.matrix.shape(matrix)
lib.matrix.transpose(matrix)
lib.matrix.add(a, b)
lib.matrix.multiply(a, b)
```

`shape(matrix)` returns `[rowCount, columnCount]`.

`transpose(matrix)` returns a new matrix whose rows and columns are exchanged. The
returned rows do not alias the input rows.

`add(a, b)` requires equal matrix shapes and returns a new matrix containing
element-wise sums.

`multiply(a, b)` performs standard row-by-column multiplication. If `a` has shape
`m x n`, `b` must have shape `n x p`, and the result has shape `m x p`. Equivalently,
the number of columns in `a` must equal the number of rows in `b`.

Matrix addition and multiplication are library functions only. The core `+` and `*`
operators are not overloaded for matrix-shaped nested lists in this version.

Example:

```vec
import lib.matrix;

let a = [[1, 2], [3, 4]];
let b = [[5, 6], [7, 8]];

print(lib.matrix.shape(a));       // [2, 2]
print(lib.matrix.transpose(a));   // [[1, 3], [2, 4]]
print(lib.matrix.add(a, b));      // [[6, 8], [10, 12]]
print(lib.matrix.multiply(a, b)); // [[19, 22], [43, 50]]
```


### 13.13 External C# plugin modules

An embedding host, the CLI, or REPL startup may explicitly load one or more supported
external C# plugin assemblies before Vector code executes. Plugin loading is a host action,
not part of Vector source syntax.

For example, the host may load a DLL that registers the qualified module `example.tools`.
Vector source still uses the ordinary module rules:

```vec
import example.tools;

print(example.tools.double(21));
```

External plugin modules are native modules for language purposes:

- they occupy ordinary qualified module ids;
- they must be imported before their members are used;
- imports do not flatten plugin members into global names;
- native call arity and runtime behavior are unchanged;
- a source module and a plugin/native module with the same id produce `ModuleConflict`;
- two native/plugin registrations cannot silently replace one another.

A plugin may export several modules, and several explicitly loaded plugins may coexist in one
runtime when their plugin ids and module ids do not conflict. Plugin API compatibility,
assembly loading, dependency resolution, and registration failures are host/plugin concerns;
plugin-function calls that reach the Vector runtime use the existing native-call diagnostic
boundary.

Vector does not auto-scan directories for plugins, does not expose arbitrary public C# methods,
and does not define source syntax such as `loadPlugin(...)`. External plugins execute trusted
in-process .NET code; the language specification does not define a sandbox for them. C# authoring
and deployment details are documented in `docs/PLUGIN_DEVELOPMENT.md`.

## 14. Core built-ins

The following built-ins are globally available when no ordinary lexical binding of
the same name shadows them.

Built-in arity is strict. Declaring a variable or function with the same name may
shadow a built-in in that lexical scope; built-ins themselves are not assignable
ordinary bindings.

### 14.1 `print(value)`

Writes one formatted value followed by a newline and returns `nothing`.

Vector v1 display formatting:

- number -> invariant-culture numeric text;
- top-level text -> text contents without surrounding quotes;
- boolean -> `true` or `false`;
- `nothing` -> `nothing`;
- list -> bracketed comma-separated recursive display;
- function -> `<function>`.

Text values nested inside displayed lists retain double quotes so list structure and
text values remain unambiguous. This rule applies recursively to nested lists. A
top-level text value is still printed without surrounding quotes.

Examples:

```vec
print(20);
print("hello");
print([1, "two", true, ["nested"]]);
```

Output:

```text
20
hello
[1, "two", true, ["nested"]]
```

### 14.2 `length(value)`

Accepts a list or text.

- list -> element count;
- text -> count of Unicode scalar values (Unicode runes), not UTF-16 code units.

Other value types are runtime errors.

### 14.3 `concat(listA, listB)`

Requires two lists and returns a new shallow list containing the elements of the
first followed by the second.

### 14.4 `text(value)`

Returns text using the same value display rules as `print`, but without writing to
the host. Passing text returns the text value unchanged.

### 14.5 `number(value)`

Accepts:

- a number -> returned unchanged;
- text containing a finite invariant-culture number -> parsed to `number`.

Other values or non-numeric/non-finite text produce a runtime error.

### 14.6 `type(value)`

Returns a text value naming the current public runtime type. The exact returned names
are:

```text
number
text
boolean
list
function
nothing
```

The result reflects the runtime value model, not higher-level library conventions.
Numeric lists, vector arguments, and matrix-shaped nested lists therefore all report
`list`.

`type` has arity one and may be shadowed by an ordinary lexical binding just like the
other core built-ins.

### 14.7 `range(start, end)`

Both arguments must be finite whole numbers representable by the implementation's
integer range.

The result is an ascending list with `start` inclusive and `end` exclusive:

```vec
range(1, 4); // [1, 2, 3]
```

If `start >= end`, the result is `[]`. Descending ranges and a custom step are not
part of v1.

## 15. Errors and diagnostics

Vector distinguishes lexical, syntax/context, module, name/scope, and runtime
failures.

Diagnostics are structured and include:

```text
code
message
severity
source span (offset, line, column)
source identity/file when available
source text when available
```

The command-line formatter presents diagnostics in the general form:

```text
path\program.vec:2:1: error RuntimeTypeError: ...
    value + "bad";
    ^^^^^^^^^^^^^
```

Errors raised while executing or calling code from an imported module retain the
originating module file/source information.

### 15.1 Lexical and parse errors

The lexer/parser recover where practical and may report multiple independent
problems. Invalid parsed source is not executed.

### 15.2 Runtime errors

A runtime error stops normal execution of the current file/program execution.
Examples include:

- undefined variable lookup or assignment;
- invalid operand types;
- non-boolean conditions;
- division/remainder by zero;
- invalid list indexing;
- vector length mismatch;
- cyclic-list creation;
- calling a non-function;
- wrong function or native-call argument count;
- module loading/circular-import failures;
- source/native module-name conflicts;
- native argument conversion/type failures;
- native operations that return non-finite numeric results.

Native failures are translated to structured Vector diagnostics. The Vector call-site
span is retained for native call failures, and unexpected host exceptions are reported
without exposing raw C# exception details or stack traces.

Vector does not guess, coerce, or silently repair invalid operations.

### 15.3 REPL errors

A lexical, syntax, module, or runtime failure aborts the current REPL submission but
does not terminate the REPL process. State established by earlier successful
submissions remains available.

## 16. Execution interfaces

### 16.1 File execution

Conceptually:

```text
vector program.vec
```

With the repository CLI project:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- program.vec
```

The runner requires at most one `.vec` argument and uses strict UTF-8 input. The entry
file's directory becomes the module root. The normal CLI runtime also uses the default native
standard-library registry, which includes `lib.math`, `lib.collections`, `lib.io`, `lib.vector`,
and `lib.matrix`. The CLI host supplies line input, so `lib.io.readLine()` reads from standard
input in file mode.

Backend selection is a host/CLI concern. The interpreter remains the default and reference
implementation:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- program.vec
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- --engine interpreter program.vec
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- --engine vm program.vec
```

Both backends target the same Vector source semantics, module rules, built-ins, standard
library, plugin boundary, evaluation order, and structured diagnostic behavior.
Backend selection does not introduce any Vector keyword, statement, expression, or grammar rule.

The VM backend also supports a compile/disassembly-only CLI mode:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- --engine vm --disassemble program.vec
```

`--disassemble` requires a source file and `--engine vm`. It parses/compiles and prints
deterministic bytecode without executing program side effects.

Before the optional source file, the CLI accepts repeated explicit plugin options:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- --plugin PluginA.dll --plugin PluginB.dll program.vec
```

Each `--plugin` requires one following DLL path. Plugin loading/setup failures are CLI setup
failures (exit code `2`) rather than Vector-language runtime failures. Plugin DLL paths are not
read from Vector source and are never discovered by directory scanning.

Process exit codes:

```text
0  success
1  Vector lexical/syntax/module/runtime failure
2  CLI or file-input failure
```

### 16.2 REPL

Launching the CLI without a source file starts the REPL. With no explicit backend, it
uses the interpreter:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj
```

A persistent VM-backed REPL is selected with:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- --engine vm
```

Each VM REPL submission is parsed and compiled separately, while the VM session reuses
its top-level lexical environment, module loader, imported modules, functions, closures,
and module state across successful submissions.

A REPL may also start with one or more explicit trusted plugins:

```powershell
dotnet run --project src/Vector.Cli/Vector.Cli.csproj -- --plugin ExamplePlugin.dll
```

Plugins are loaded before the REPL starts and their registered modules remain available for the
REPL session, subject to the same explicit `import` requirement as every other module.

Example:

```text
Vector REPL. Type :exit or :quit to leave.
vector> let x = 10;
vector> x;
10
```

REPL rules:

- successful submissions share one top-level environment;
- functions/variables persist between submissions;
- imported source and native module state is retained for that REPL session;
- the default native standard-library registry (`lib.math`, `lib.collections`,
  `lib.io`, `lib.vector`, and `lib.matrix`) is available;
- the REPL input stream is also the input source used by `lib.io.readLine()`;
- an expression statement whose final value is not `nothing` is displayed;
- unmatched `(`, `{`, or `[` causes continuation input with the `...> ` prompt;
- `:exit` and `:quit` exit when entered as a top-level REPL command;
- module paths resolve relative to the directory from which the REPL was created;
- the interpreter and VM REPLs follow the same Vector-language rules; differences in
  bytecode/disassembly are implementation/debugging details rather than language semantics.

The reusable host API follows the same distinction: `VectorEngine` executes with the
reference interpreter, while `VectorVmEngine` executes with the bytecode VM and can create
a persistent `VectorVmSession`. Selecting one of these host APIs does not alter Vector syntax.

## 17. Formal grammar

The following EBNF-style grammar defines Vector v1 syntax.

```text
program
    -> importDeclaration* declaration* EOF ;

importDeclaration
    -> "import" modulePath ";" ;

modulePath
    -> IDENTIFIER ( "." IDENTIFIER )* ;

declaration
    -> functionDeclaration
     | letDeclaration
     | statement ;

functionDeclaration
    -> "function" IDENTIFIER "(" parameters? ")" block ;

parameters
    -> IDENTIFIER ( "," IDENTIFIER )* ;

letDeclaration
    -> "let" IDENTIFIER "=" expression ";" ;

statement
    -> block
     | ifStatement
     | whileStatement
     | forStatement
     | returnStatement
     | breakStatement
     | continueStatement
     | expressionStatement ;

block
    -> "{" declaration* "}" ;

ifStatement
    -> "if" expression block
       ( "else" ( ifStatement | block ) )? ;

whileStatement
    -> "while" expression block ;

forStatement
    -> "for" IDENTIFIER "in" expression block ;

returnStatement
    -> "return" expression? ";" ;

breakStatement
    -> "break" ";" ;

continueStatement
    -> "continue" ";" ;

expressionStatement
    -> expression ";" ;

expression
    -> assignment ;

assignment
    -> logicOr ( "=" assignment )? ;

logicOr
    -> logicAnd ( "or" logicAnd )* ;

logicAnd
    -> equality ( "and" equality )* ;

equality
    -> comparison ( ( "==" | "!=" ) comparison )* ;

comparison
    -> term ( ( "<" | "<=" | ">" | ">=" ) term )* ;

term
    -> factor ( ( "+" | "-" ) factor )* ;

factor
    -> unary ( ( "*" | "/" | "%" ) unary )* ;

unary
    -> ( "not" | "-" ) unary
     | postfix ;

postfix
    -> primary
       ( "(" arguments? ")"
       | "[" expression "]"
       | "." IDENTIFIER
       )* ;

arguments
    -> expression ( "," expression )* ;

primary
    -> NUMBER
     | STRING
     | "true"
     | "false"
     | "nothing"
     | IDENTIFIER
     | listLiteral
     | "(" expression ")" ;

listLiteral
    -> "[" ( expression ( "," expression )* )? "]" ;
```

The assignment grammar is intentionally broad. Vector v1 semantic validation only
permits these assignment target shapes:

```text
identifier
list indexing expression
```

Examples:

```vec
x = 10;
values[0] = 10;
```

Assignment to literals, call results, or imported module members is invalid.

Context rules additionally require:

- imports only at top level and before other top-level declarations/statements;
- `return` inside a function;
- `break`/`continue` inside a loop;
- unique function parameter names.

## 18. Future natural-language compatibility

Formal Vector source remains the canonical inspectable representation.

A future front end may translate:

```text
Create a variable called x with the value 10, then display x.
```

into:

```vec
let x = 10;
print(x);
```

The future layer may accept many human phrasings, but generated behavior should map
to the deterministic semantics in this specification. Generated Vector should remain
visible for inspection whenever practical.

## 19. Version 1 non-goals

Vector v1 does not require:

- unrestricted natural-language parsing in the core language;
- static type declarations;
- implicit type coercion or truthiness;
- classes or a general object/member system;
- arbitrary .NET API access or automatic reflection-based library discovery;
- arbitrary external DLL/plugin loading;
- package management or package publishing;
- a production-scale standard library beyond the implemented initial modules;
- dedicated `vector` or `matrix` runtime value kinds;
- matrix operator overloading in the core `+` or `*` operators;
- bytecode/native compilation;
- an integrated debugger;
- a custom IDE.

These can be explored after the interpreter MVP without changing the core strictness,
lexical-scope, and diagnostic principles above.
