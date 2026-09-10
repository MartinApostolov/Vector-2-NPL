import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import * as vscode from 'vscode';

export function activate(): void {
  setTimeout(() => { void run(); }, 0);
}

async function run(): Promise<void> {
  const resultPath = process.env.VECTOR_INSTALLED_TEST_RESULT;

  try {
    assert.ok(resultPath, 'VECTOR_INSTALLED_TEST_RESULT was not provided.');
    const extension = vscode.extensions.getExtension('martinapostolov.vector-language-support');
    assert.ok(extension, 'The packaged Vector extension was not installed.');
    await extension.activate();

    const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri;
    assert.ok(workspaceRoot, 'The installed VSIX test workspace was not opened.');
    const document = await vscode.workspace.openTextDocument(vscode.Uri.joinPath(workspaceRoot, 'activation.vec'));
    const editor = await vscode.window.showTextDocument(document);
    assert.equal(document.languageId, 'vector');
    await replaceDocument(editor, 'let missingExpression = ;');
    await waitUntil(
      () => vscode.languages.getDiagnostics(document.uri).some(diagnostic => diagnostic.code === 'ExpectedExpression'),
      'the packaged extension to publish parser diagnostics'
    );

    await replaceDocument(editor, 'pri');
    const builtinCompletion = await getCompletionItems(document, new vscode.Position(0, 3));
    assert.ok(
      builtinCompletion.some(item => item.label === 'print' && item.kind === vscode.CompletionItemKind.Function),
      'The installed extension did not offer the print built-in.'
    );

    await replaceDocument(editor, 'le');
    const keywordCompletion = await getCompletionItems(document, new vscode.Position(0, 2));
    assert.ok(
      keywordCompletion.some(item => item.label === 'let' && item.kind === vscode.CompletionItemKind.Keyword),
      'The installed extension did not offer the let keyword.'
    );

    const memberSource = 'import local.geometry;\nlocal.geometry.';
    await replaceDocument(editor, memberSource);
    const memberCompletion = await getCompletionItems(
      document,
      new vscode.Position(1, 'local.geometry.'.length)
    );
    assert.ok(memberCompletion.some(item => item.label === 'origin' && item.kind === vscode.CompletionItemKind.Variable));
    assert.ok(memberCompletion.some(item => item.label === 'rectangleArea' && item.kind === vscode.CompletionItemKind.Function));

    await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');
    await writeFile(resultPath, 'PASS: 3 editor completions and live diagnostics', 'utf8');
  } catch (error) {
    if (resultPath !== undefined) {
      await writeFile(resultPath, `FAIL: ${error instanceof Error ? error.stack ?? error.message : String(error)}`, 'utf8');
    }
  } finally {
    await vscode.commands.executeCommand('workbench.action.quit');
  }
}

async function replaceDocument(editor: vscode.TextEditor, text: string): Promise<void> {
  await editor.edit(builder => {
    builder.replace(
      new vscode.Range(0, 0, editor.document.lineCount, editor.document.lineAt(editor.document.lineCount - 1).text.length),
      text
    );
  });
}

async function getCompletionItems(
  document: vscode.TextDocument,
  position: vscode.Position
): Promise<readonly vscode.CompletionItem[]> {
  const completion = await vscode.commands.executeCommand<vscode.CompletionList>(
    'vscode.executeCompletionItemProvider',
    document.uri,
    position
  );
  return completion.items;
}

async function waitUntil(predicate: () => boolean, description: string): Promise<void> {
  const deadline = Date.now() + 10_000;
  while (!predicate()) {
    if (Date.now() >= deadline) {
      throw new Error(`Timed out waiting for ${description}.`);
    }
    await new Promise(resolve => setTimeout(resolve, 50));
  }
}
