# Vector Language Specification

**Status:** Initial language design for Vector v1  
**Project:** Vector-2-NPL  
**File extension:** `.vec`

## 1. Purpose and Design Principles

Vector is a small general-purpose programming language whose first implementation is
a C# tree-walking interpreter.

Vector v1 uses a formal, deterministic syntax. A future natural-language front end
may translate human instructions into formal Vector source code or into the same
canonical syntax/semantic representation used by the interpreter. Natural-language
interpretation is intentionally kept outside the core parser.

The language is designed around these principles:

1. Formal Vector remains strict and deterministic.
2. Syntax should be readable and unsurprising to programmers and approachable to
   beginners.
3. Variables are dynamically typed; values have runtime types.
4. Operations are strict about the runtime types they accept.
5. Vector performs no implicit type coercion.
6. Conditions require actual boolean values; there is no truthiness.
7. Evaluation order is defined and deterministic.
8. Modules are explicit and accessed through their full qualified paths.
9. Diagnostics carry precise source locations.
10. Future front ends, interpreters, virtual machines, IDE tooling, and natural-
    language systems should reuse the same core semantics.

The initial execution pipeline is:

```text
Vector source
-> Lexer
-> Parser
-> AST
-> Semantic/runtime checks
-> Tree-walking interpreter
-> Result
```

A future natural-language pipeline may be:

```text
Natural-language instruction
-> NLP/AI translation
-> inspectable Vector source
-> normal Vector pipeline
```

## 2. Source Files and Encoding

Vector source files use the `.vec` extension.

Source files are UTF-8 text. Unicode text and Unicode identifiers are supported.

Whitespace separates tokens but is otherwise insignificant. Newlines do not end
statements. Normal statements are terminated by `;`.

Example:

```vec
let name = "Vector";
let count = 3;

print(name);
```

## 3. Lexical Rules

### 3.1 Identifiers

Identifiers may use Unicode letters.

Recommended lexical rules:

- The first character must be `_` or a Unicode letter.
- Later characters may be `_`, Unicode letters, Unicode combining marks, or digits.
- Identifier comparison is case-sensitive.
- Identifiers are normalized to Unicode NFC before name comparison.

Examples of valid identifiers:

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

Keywords use their exact lowercase spelling.

### 3.2 Keywords

Vector v1 reserves these keywords:

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

Additional keywords may be introduced by later language versions.

### 3.3 Numbers

Vector exposes one user-facing numeric type: `number`.

Examples:

```vec
0
20
3.14
1000.5
1e3
2.5e-4
```

A leading `-` is a unary operator, not part of the numeric literal.

A decimal point must have digits on both sides. Therefore:

```text
0.5   valid
5.0   valid
.5    invalid
5.    invalid
```

Numeric separators are not part of Vector v1.

The implementation may use an appropriate internal numeric representation, but
the language exposes ordinary numeric values as `number`.

Division is mathematical rather than integer division:

```vec
5 / 2
```

evaluates to:

```text
2.5
```

Division by zero is a runtime error.

### 3.4 Text

Text literals use double quotes:

```vec
"hello"
"Здравей"
```

Single-quoted strings are not part of Vector v1.

Supported escape sequences are:

```text
\"   double quote
\\   backslash
\n   newline
\r   carriage return
\t   tab
```

Text literals may contain Unicode directly. Multiline text literals are not part
of Vector v1.

### 3.5 Boolean and Nothing Literals

Boolean literals are:

```vec
true
false
```

The no-value literal is:

```vec
nothing
```

`nothing` is a real runtime value with its own type. It is not false and does not
participate in truthiness.

### 3.6 Comments

Single-line comments begin with `//`:

```vec
// This is a comment.
let x = 10;
```

Block comments use `/*` and `*/`:

```vec
/*
    This is a block comment.
*/
let x = 10;
```

Block comments do not nest in Vector v1.

## 4. Runtime Value Model

Vector is dynamically typed.

A variable does not permanently own a type. The value currently stored in the
variable has a runtime type.

Example:

```vec
let value = "20";

value = 20;
```

Both assignments are valid.

However, operations check the current runtime value:

```vec
let value = "20";

value + 5;   // runtime type error

value = 20;

value + 5;   // valid, result is 25
```

Vector v1 has these core runtime value categories:

```text
number
text
boolean
list
function
nothing
```

Modules also exist as language namespaces, but imported module paths are not
ordinary variable values in Vector v1.

### 4.1 No Implicit Coercion

Vector does not silently convert between unrelated types.

```vec
5 + "2"
```

is an error.

```vec
"Age: " + 20
```

is an error.

Explicit conversion functions may be provided by the standard library, for
example `text(value)` and `number(value)`, but conversion is never implicit.

### 4.2 Text Concatenation

`+` concatenates two text values:

```vec
"Hello " + "Vector"
```

evaluates to:

```text
"Hello Vector"
```

Both operands must be text.

### 4.3 Lists

Lists use square brackets:

```vec
let values = [1, 2, 3];
let names = ["Alice", "Bob"];
let mixed = [1, "hello", true, nothing];
let nested = [[1, 2], [3, 4]];
```

Lists are ordered, zero-indexed, and mutable.

#### Indexing

```vec
let values = [10, 20, 30];

values[0];   // 10
values[1];   // 20
```

An index must be a non-negative whole number and must be within the list bounds.

These are runtime errors:

```vec
values[-1];
values[1.5];
values[10];
```

List elements may be replaced:

```vec
values[1] = 50;
```

Vector v1 does not support cyclic list structures. An operation that would make a
list directly or indirectly contain itself must fail with a runtime error.

### 4.4 Numeric Lists and Vector Behavior

A list is always a `list`.

It does not permanently change into a separate vector type.

Instead, a list whose current elements are all numbers is a **numeric list** and
supports vector operations.

Example:

```vec
let values = [1, 2, 3];

values * 2;   // [2, 4, 6]
```

If the contents change:

```vec
values[1] = "two";

values * 2;   // runtime type error
```

If the contents later become numeric again, vector operations become valid again.

This rule is based on the current contents of the list, matching Vector's dynamic
runtime type model.

The empty list counts as a numeric list of length zero.

#### Vector Addition

Two numeric lists of equal length may be added element by element:

```vec
[1, 2] + [3, 4]
```

evaluates to:

```vec
[4, 6]
```

Different lengths are a runtime error.

#### Vector Subtraction

Two numeric lists of equal length may be subtracted element by element:

```vec
[1, 2] - [3, 4]
```

evaluates to:

```vec
[-2, -2]
```

Different lengths are a runtime error.

#### Scalar Multiplication

A numeric list may be multiplied by a number in either order:

```vec
[1, 2, 3] * 2
2 * [1, 2, 3]
```

Both evaluate to:

```vec
[2, 4, 6]
```

List/list multiplication and other matrix operations are not defined by Vector v1.

#### List Concatenation

`+` is not general list concatenation.

For numeric lists, `+` means vector addition. For other lists, `+` is a type error.

General list concatenation is explicit and may be provided through the standard
library:

```vec
concat([1, 2], [3, 4])
```

which would produce:

```vec
[1, 2, 3, 4]
```

This keeps mathematical vector operations unambiguous.

Future matrix operations may build on lists of equal-length numeric lists without
requiring a separate matrix literal syntax.

## 5. Variables and Assignment

Variables are declared with `let`:

```vec
let x = 10;
let name = "Vector";
let enabled = true;
```

A declaration requires an initializer.

Assignment changes an existing binding:

```vec
x = 20;
```

Assignment to an undeclared identifier is an error:

```vec
missing = 20;   // error
```

The right-hand expression is evaluated before the new binding is introduced.

### 5.1 Redeclaration

Declaring the same name twice in the same scope is an error:

```vec
let x = 10;
let x = 20;   // error
```

Assignment is valid:

```vec
let x = 10;
x = 20;
```

### 5.2 Shadowing

A nested scope may declare a new binding with the same name:

```vec
let x = 10;

if true {
    let x = 20;
    print(x);   // 20
}

print(x);       // 10
```

### 5.3 Assignment to Outer Bindings

Assignment resolves the nearest existing lexical binding.

```vec
let counter = 0;

function increase() {
    counter = counter + 1;
}
```

Calling `increase()` changes the outer `counter`.

## 6. Expressions and Operators

### 6.1 Arithmetic

Numeric arithmetic operators are:

```text
+
-
*
/
%
```

Examples:

```vec
2 + 3;
8 - 5;
4 * 3;
5 / 2;
10 % 3;
```

Unary negation is supported:

```vec
-5
```

Arithmetic operands must satisfy the operation's runtime type rules.

### 6.2 Comparison

Comparison operators are:

```text
<
<=
>
>=
```

Vector v1 defines ordering comparisons for numbers.

```vec
5 < 10
3.5 >= 3
```

Ordering unrelated runtime types is an error.

### 6.3 Equality

Equality operators are:

```text
==
!=
```

Equality does not coerce types.

```vec
5 == 5          // true
5 == "5"        // false
"abc" == "abc"  // true
```

Lists compare their contents recursively:

```vec
[1, 2] == [1, 2]   // true
[1, 2] == [2, 1]   // false
```

`nothing == nothing` is `true`.

Values of different runtime types compare as unequal.

Function values compare by identity.

### 6.4 Logical Operators

Logical operators are words:

```text
and
or
not
```

They operate only on booleans.

```vec
true and false
true or false
not true
```

There is no truthiness.

### 6.5 Strict Boolean Conditions

Conditions in `if` and `while` must evaluate to a boolean.

Valid:

```vec
if age >= 18 {
    print("Adult");
}
```

Valid when `loggedIn` currently contains a boolean:

```vec
if loggedIn {
    print("Welcome");
}
```

Invalid:

```vec
if 5 {
    print("Invalid");
}

if "hello" {
    print("Invalid");
}
```

### 6.6 Short-Circuit Evaluation

`and` and `or` short-circuit.

```vec
false and dangerousCall()
```

does not call `dangerousCall()`.

```vec
true or dangerousCall()
```

also does not call `dangerousCall()`.

### 6.7 Evaluation Order

Expression operands and function arguments are evaluated left-to-right.

```vec
foo(first(), second());
```

calls `first()` before `second()`.

### 6.8 Operator Precedence

From highest to lowest:

| Precedence | Operators / forms |
| --- | --- |
| 1 | Grouping `(...)`, calls `(...)`, indexing `[...]`, qualified access `.` |
| 2 | Unary `not`, unary `-` |
| 3 | `*`, `/`, `%` |
| 4 | `+`, `-` |
| 5 | `<`, `<=`, `>`, `>=` |
| 6 | `==`, `!=` |
| 7 | `and` |
| 8 | `or` |
| 9 | Assignment `=` |

Examples:

```vec
2 + 3 * 4;       // 14
(2 + 3) * 4;     // 20
```

Assignment associates right-to-left. The other binary operators above associate
left-to-right.

## 7. Blocks and Scope

Blocks use braces:

```vec
{
    let x = 10;
    print(x);
}
```

Blocks create lexical scopes.

A name declared inside a block is not visible after the block ends:

```vec
{
    let x = 10;
}

print(x);   // error
```

Function calls create function-local scopes whose parent is the lexical
environment captured when the function was declared.

## 8. Conditional Statements

Conditions do not require parentheses.

```vec
if score >= 90 {
    print("A");
} else if score >= 80 {
    print("B");
} else {
    print("C");
}
```

Parentheses may still be used for grouping:

```vec
if (score >= 80 and score < 90) {
    print("B");
}
```

Only the selected branch executes.

## 9. Loops

### 9.1 While

```vec
while x < 10 {
    x = x + 1;
}
```

The condition is checked before each iteration and must be boolean.

### 9.2 For-In

Vector supports iteration over lists:

```vec
for item in items {
    print(item);
}
```

The iterable expression is evaluated once when the loop starts.

The values to iterate are captured as a shallow snapshot at loop start. Structural
changes to the original list during the loop do not change which elements this
iteration will visit.

The loop variable is local to the loop and a fresh iteration scope is used for
each iteration.

Numeric iteration may later be expressed using a standard-library function such as:

```vec
for number in range(1, 10) {
    print(number);
}
```

### 9.3 Break and Continue

`break` exits the nearest enclosing loop:

```vec
while true {
    if done {
        break;
    }
}
```

`continue` skips to the next iteration of the nearest enclosing loop:

```vec
for item in items {
    if shouldSkip(item) {
        continue;
    }

    print(item);
}
```

Using `break` or `continue` outside a loop is an error.

## 10. Functions

Functions are declared with the `function` keyword:

```vec
function add(a, b) {
    return a + b;
}
```

Functions are called with parentheses:

```vec
let result = add(5, 3);
```

### 10.1 Parameters and Arity

Parameters are dynamically typed local bindings.

Argument count is strict:

```vec
function add(a, b) {
    return a + b;
}

add(1);       // error
add(1, 2, 3); // error
```

Duplicate parameter names are an error.

Vector v1 does not require or provide type annotations:

```vec
function add(a, b) {
    return a + b;
}
```

is the canonical form.

### 10.2 Return

A function may return a value:

```vec
function square(x) {
    return x * x;
}
```

A bare return is allowed and returns `nothing`:

```vec
return;
```

If execution reaches the end of a function without executing `return`, the
function returns `nothing`.

Using `return` outside a function is an error.

### 10.3 Functions as Values

Functions are runtime values.

```vec
function add(a, b) {
    return a + b;
}

let operation = add;
let result = operation(2, 3);
```

Named functions may be declared in lexical scopes. A function captures the lexical
environment in which it is declared, allowing closures.

Function declarations are not hoisted. A function binding becomes available when
its declaration executes. The function's own name is available inside its body so
that recursion works.

## 11. Top-Level Execution

Vector does not require a `main()` function.

A source file may contain executable top-level code:

```vec
let x = 5;
print(x);
```

When a file is launched directly, its top-level statements execute in source order.

## 12. Modules and Multiple Files

Every `.vec` file is a module.

A program may import local modules:

```vec
import lib.geometry;
```

If the program entry file is:

```text
MyProgram/main.vec
```

then:

```vec
import lib.geometry;
```

resolves to:

```text
MyProgram/lib/geometry.vec
```

The directory containing the launched entry file is the Vector v1 program root.

Imports use qualified module paths made from identifiers separated by `.`.

### 12.1 Full Qualified Access

Imported members are accessed through the module's full path.

```vec
import lib.geometry;

let distance = lib.geometry.distance(a, b);
```

Vector does not shorten that automatically to:

```vec
geometry.distance(a, b);
```

This is intentional. Full qualification keeps dependencies explicit and avoids
collisions between modules with the same final name.

For example:

```vec
import game.geometry;
import math.geometry;

game.geometry.distance(a, b);
math.geometry.distance(a, b);
```

### 12.2 Module Scope

Each module has its own isolated module scope.

A module-scope declaration does not become an unqualified global in the importing
module.

For Vector v1, every module-scope declaration is accessible through the imported
module's qualified path. There is no `export` keyword in Vector v1.

Example:

```vec
// lib/geometry.vec

let pi = 3.14159;

function distance(a, b) {
    // ...
}
```

After:

```vec
import lib.geometry;
```

the importer may use:

```vec
lib.geometry.pi;
lib.geometry.distance(a, b);
```

Block-local and function-local bindings are never module members.

### 12.3 Import Placement

Imports are module-level declarations.

They must appear before the module's other top-level declarations and executable
statements.

Imports are not allowed inside functions, loops, conditionals, or other blocks in
Vector v1.

### 12.4 Module Initialization

A module's top-level code executes when the module is first imported.

Each module is initialized at most once during one program execution, even if it
is reachable through multiple imports.

### 12.5 Circular Imports

Circular module dependencies are an error in Vector v1.

Example:

```text
a.vec imports b.vec
b.vec imports a.vec
```

must produce a module diagnostic describing the dependency cycle.

### 12.6 Module Namespace

Qualified module paths occupy a module namespace separate from ordinary variable
bindings.

A dotted expression that matches an imported module path resolves through the
module namespace.

General object/member access is reserved for future language versions.

### 12.7 Future Packages and External Libraries

Vector v1 does not require a package manager or external package manifest.

The module design is intended to support future packages without changing source
semantics. A future package may provide qualified Vector modules that are imported
through the same model.

Native or .NET functionality should be exposed through a controlled host API or
standard-library modules. Vector should not expose unrestricted arbitrary .NET
reflection or `System.*` access by default.

A future project manifest may define fields such as:

```text
name
version
language version
entry file
dependencies
capabilities/permissions
```

Those features are outside Vector v1.

## 13. Core Built-Ins and Standard Library Direction

### 13.1 Required Core Built-In

Vector v1 requires:

```vec
print(value);
```

`print` writes one value followed by a newline.

Its basic display behavior is:

- `number` -> culture-independent numeric text
- `text` -> the text contents without surrounding quotes
- `boolean` -> `true` or `false`
- `nothing` -> `nothing`
- `list` -> bracketed list notation
- `function` -> an implementation-defined descriptive function representation

Example:

```vec
print(20);
print("hello");
print([1, 2, 3]);
```

may display:

```text
20
hello
[1, 2, 3]
```

Strings nested inside displayed lists should retain quotes so list structure remains
unambiguous.

### 13.2 Planned Library Functions

The following names are useful candidates for the early standard library but are
not all required by the first interpreter milestone:

```text
text(value)
number(value)
length(value)
concat(a, b)
range(start, end)
type(value)
```

Later functionality should prefer clear modules rather than placing a large standard
library into the global namespace.

Possible future modules include:

```vec
import math;
import text;
import io;
import collections;
```

## 14. Errors and Diagnostics

Vector distinguishes lexical, syntax, module, name/scope, and runtime errors.

Diagnostics should contain structured source information rather than being plain
strings.

The implementation should track at least:

```text
SourcePosition
    line
    column
    absolute offset

SourceSpan
    start
    end

Diagnostic
    code
    message
    source file
    span
```

Example presentation:

```text
VECxxxx: Cannot add number and text.
main.vec:4:12

let total = price + "5";
                    ^^^
```

Exact diagnostic codes will be assigned during implementation.

### 14.1 Lexical and Parse Errors

The lexer and parser should recover where practical so one pass may report more
than one independent source error.

Invalid source must not be executed.

### 14.2 Runtime Errors

A runtime error stops normal execution of the current program.

Examples include:

- using an undeclared variable
- assigning to an undeclared variable
- invalid operand types
- non-boolean conditions
- division by zero
- invalid list indexing
- incompatible numeric-list lengths
- wrong function argument count
- illegal `return`, `break`, or `continue`
- module loading failures
- circular imports

The interpreter must not guess, coerce, or silently repair invalid operations.

### 14.3 REPL Errors

A runtime or syntax error in the REPL aborts the current submitted input but does
not terminate the REPL process itself.

Previously completed REPL declarations remain available.

## 15. Execution Interfaces

Vector supports two execution modes.

### 15.1 File Execution

```text
vector program.vec
```

The supplied file becomes the entry module.

Its containing directory becomes the program root for local module resolution.

### 15.2 REPL

Launching Vector without a file starts the interactive REPL:

```text
vector
> let x = 10;
> print(x);
10
```

REPL submissions share a persistent top-level environment.

The REPL may accept multiline input when a block or expression is incomplete.

## 16. Formal Grammar

The following EBNF-style grammar defines the intended Vector v1 syntax.

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

Assignment syntax is intentionally broad in the grammar. Semantic validation
restricts assignable targets in Vector v1 to:

```text
identifier
list indexing expression
```

For example:

```vec
x = 10;
values[0] = 10;
```

are valid assignment shapes.

Assignment to a function call result, literal, or imported module member is not
valid in Vector v1.

## 17. Future Natural-Language Compatibility

Formal Vector is the canonical inspectable representation.

A future user may write:

```text
Create a variable called x with the value 10, then display x.
```

A natural-language front end may produce:

```vec
let x = 10;
print(x);
```

Similarly:

```text
For each positive number in values, add it to the total.
```

may become formal Vector such as:

```vec
for value in values {
    if value > 0 {
        total = total + value;
    }
}
```

The future natural-language layer may accept many human phrasings, but they should
map to one deterministic set of Vector semantics.

Generated Vector source should remain visible before execution whenever practical
so users can inspect what the natural-language system understood.

## 18. Version 1 Non-Goals

The initial language design does not require:

- unrestricted natural-language parsing in the core language
- static type declarations
- implicit type coercion
- truthiness
- classes or a general object system
- arbitrary .NET API access
- a package manager
- package publishing
- a production-scale standard library
- bytecode or native compilation
- an integrated debugger
- matrix operations beyond the numeric-list foundation
- a custom IDE

These may be added later without changing the core principles above.
