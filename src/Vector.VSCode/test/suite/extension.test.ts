import assert from 'node:assert/strict';
import * as vscode from 'vscode';

async function waitUntil(predicate: () => boolean, description: string): Promise<void> {
  const deadline = Date.now() + 10_000;
  while (!predicate()) {
    if (Date.now() >= deadline) {
      throw new Error(`Timed out waiting for ${description}.`);
    }
    await new Promise(resolve => setTimeout(resolve, 50));
  }
}

suite('Vector VS Code extension', () => {
  test('activates and registers the Vector language and commands', async () => {
    const extension = vscode.extensions.getExtension('martinapostolov.vector-language-support');
    assert.ok(extension, 'Vector extension was not discovered by the Extension Development Host.');
    await extension.activate();
    assert.equal(extension.isActive, true);

    const languages = await vscode.languages.getLanguages();
    assert.ok(languages.includes('vector'));
    const commands = await vscode.commands.getCommands(true);
    for (const command of [
      'vector.runCurrentFile',
      'vector.runCurrentFileWithInterpreter',
      'vector.runCurrentFileWithVm',
      'vector.showBytecode'
    ]) {
      assert.ok(commands.includes(command), `${command} was not registered.`);
    }
  });

  test('starts the official language client and updates live diagnostics', async () => {
    const document = await vscode.workspace.openTextDocument({
      language: 'vector',
      content: 'pri\nlet missingExpression = ;'
    });
    const editor = await vscode.window.showTextDocument(document);
    await waitUntil(
      () => vscode.languages.getDiagnostics(document.uri).some(diagnostic => diagnostic.severity === vscode.DiagnosticSeverity.Error),
      'a Vector parser diagnostic'
    );

    const completion = await vscode.commands.executeCommand<vscode.CompletionList>(
      'vscode.executeCompletionItemProvider',
      document.uri,
      new vscode.Position(0, 3)
    );
    assert.ok(
      completion.items.some(item => item.label === 'print' && item.kind === vscode.CompletionItemKind.Function),
      'The Vector language-server completion provider did not return print.'
    );

    await editor.edit(builder => {
      builder.replace(new vscode.Range(0, 0, document.lineCount, 0), 'let missingExpression = 1;');
    });
    await waitUntil(
      () => vscode.languages.getDiagnostics(document.uri).length === 0,
      'Vector diagnostics to clear'
    );
    await vscode.commands.executeCommand('workbench.action.closeActiveEditor');
  });

  test('offers language-server completion through the editor suggest UI', async () => {
    const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri;
    assert.ok(workspaceRoot);
    const document = await vscode.workspace.openTextDocument(vscode.Uri.joinPath(workspaceRoot, 'activation.vec'));
    const editor = await vscode.window.showTextDocument(document);
    await editor.edit(builder => {
      builder.replace(new vscode.Range(0, 0, document.lineCount, 0), 'pri');
    });
    editor.selection = new vscode.Selection(0, 3, 0, 3);

    await vscode.commands.executeCommand('editor.action.triggerSuggest');
    await new Promise(resolve => setTimeout(resolve, 500));
    await vscode.commands.executeCommand('acceptSelectedSuggestion');

    assert.equal(document.getText(), 'print');
    await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');
  });

  test('runs registered commands from the unsaved editor buffer', async () => {
    const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri;
    assert.ok(workspaceRoot);
    const document = await vscode.workspace.openTextDocument(vscode.Uri.joinPath(workspaceRoot, 'activation.vec'));
    const editor = await vscode.window.showTextDocument(document);
    const unsavedSource = 'let value = 8;\nprint(value);\nvalue;';
    await editor.edit(builder => {
      builder.replace(new vscode.Range(0, 0, document.lineCount, 0), unsavedSource);
    });
    assert.equal(document.isDirty, true);

    interface CommandResponse {
      readonly success: boolean;
      readonly output: readonly string[];
      readonly result: string | null;
      readonly disassembly: string | null;
    }

    const interpreter = await vscode.commands.executeCommand<CommandResponse>(
      'vector.runCurrentFileWithInterpreter'
    );
    assert.equal(interpreter.success, true);
    assert.deepEqual(interpreter.output, ['8']);
    assert.equal(interpreter.result, '8');

    const vm = await vscode.commands.executeCommand<CommandResponse>('vector.runCurrentFileWithVm');
    assert.equal(vm.success, true);
    assert.deepEqual(vm.output, ['8']);
    assert.equal(vm.result, '8');

    const bytecode = await vscode.commands.executeCommand<CommandResponse>('vector.showBytecode');
    assert.equal(bytecode.success, true);
    assert.deepEqual(bytecode.output, []);
    assert.match(bytecode.disassembly ?? '', /Call/u);

    await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');
  });
});
