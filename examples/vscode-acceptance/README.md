# Vector VS Code acceptance workspace

Open this directory as the VS Code workspace after installing the packaged extension. The default program root can remain empty because `main.vec` and `local/geometry.vec` use the normal inherited-root layout.

## Manual checklist

1. Open `main.vec`. Confirm Vector syntax highlighting, `//` and `/* ... */` comment toggling, bracket matching, indentation after `{`, and automatic closing for `()`, `[]`, `{}`, `""`, and `''`.
2. Type `pri` in `main.vec` and confirm `print` is suggested. Hover `print`, `size`, and `local.geometry.rectangleArea`.
3. Inside `rectangleArea`, type `wid` and confirm `width` is suggested. Type `wid` outside the function and confirm `width` is not suggested there.
4. Type `local.geometry.` and confirm at least `rectangleArea` and `origin` are suggested.
5. Use F12 on `size` and on `rectangleArea`. Confirm the first navigates within `main.vec` and the second opens `local/geometry.vec`.
6. Invoke signature help inside `print(` and `local.geometry.rectangleArea(` and confirm the built-in and user-function signatures appear.
7. Find All References for `size` and `rectangleArea`. Confirm the qualified function references include both files.
8. Rename `size` locally. Undo. Rename `rectangleArea` and confirm edits occur in both `main.vec` and `local/geometry.vec`; then undo.
9. Open `malformed.vec`. Confirm a Vector parser error appears for `let missingExpression = ;`, then change it to `let missingExpression = 1;` and confirm the error clears live.
10. Run **Vector: Run Current File with Interpreter** on `main.vec`. Confirm the Vector output channel contains `Vector VS Code acceptance`, `42`, and final result `42`.
11. Run **Vector: Run Current File with VM** and confirm the same output and result.
12. Run **Vector: Show Bytecode** and confirm disassembly appears while the program prints nothing as a side effect of disassembly.
13. Change `let size = 6;` to `let size = 8;` without saving and run both engines. Confirm output and final result are `56`, proving execution used the in-memory buffer. Undo or close without saving.
14. Change `vector.defaultExecutionEngine` and use **Vector: Run Current File**. Toggle `vector.liveDiagnostics`. Test an explicit `vector.programRoot` and, if applicable, `vector.executionPluginPaths`; none should require a VS Code restart.
15. Close the Extension Development Host or VS Code and use Task Manager/Process Explorer to confirm no `Vector.LanguageServer`, `Vector.ExecutionHost`, or Vector-owned `dotnet` process remains.
