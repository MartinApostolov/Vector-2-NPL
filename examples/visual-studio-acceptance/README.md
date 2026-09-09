# Visual Studio core acceptance fixture

This folder is the manual-only fixture for the Vector Visual Studio integration gate. It is source data, not evidence that a graphical test was run.

1. Build `src/Vector.VisualStudio/Vector.VisualStudio.csproj` in `Release` or start the extension project in the Visual Studio Experimental Instance.
2. Open this folder, then open `main.vec`.
3. Confirm Vector highlighting, comment/bracket behavior, completion, hover, document symbols, module-member completion after `local.geometry.`, Go To Definition, and signature help inside `rectangleArea(`.
4. Add and remove a syntax error and confirm the Error List/squiggle clears after the edit. Open `malformed.vec` for a stable error fixture.
5. Run all four commands from the Extensions menu. Confirm interpreter and VM output in the **Vector** Output channel and confirm **Show Bytecode** does not print the program's messages.
6. In **Tools > Options > Vector**, switch the default engine, toggle live diagnostics, and optionally set this folder as the program root.
7. Configure an explicit trusted plugin DLL only if plugin execution is being tested. Confirm merely opening or editing a file does not load or execute it.

The extension has no debugger or custom `.vecproj` project system. Those are intentionally outside the v1 scope.
