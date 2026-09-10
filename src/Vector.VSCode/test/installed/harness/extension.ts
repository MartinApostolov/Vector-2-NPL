import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import * as vscode from 'vscode';

export function activate(): void {
  setTimeout(() => { void run(); }, 0);
}

async function run(): Promise<void> {
  const resultPath = process.env.VECTOR_INSTALLED_TEST_RESULT;

  try {
    if (resultPath === undefined) {
      throw new Error('VECTOR_INSTALLED_TEST_RESULT was not provided.');
    }
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

    const acceptanceSource = [
      'import local.geometry;',
      '',
      'let size = 6;',
      'let area = local.geometry.rectangleArea(size, 7);',
      'print("Vector VS Code acceptance");',
      'print(area);',
      'area;'
    ].join('\n');
    await replaceDocument(editor, acceptanceSource);
    const sizeUse = acceptanceSource.split('\n')[3]?.indexOf('size') ?? -1;
    const memberStart = acceptanceSource.split('\n')[3]?.indexOf('rectangleArea') ?? -1;
    assert.ok(sizeUse >= 0 && memberStart >= 0);

    const localReferences = await getReferences(document, new vscode.Position(3, sizeUse + 1));
    assert.equal(
      localReferences.length,
      2,
      `Expected size declaration and use; received ${JSON.stringify(localReferences.map(formatLocation))}`
    );

    const memberReferences = await getReferences(document, new vscode.Position(3, memberStart + 1));
    assert.equal(
      memberReferences.length,
      2,
      `Expected rectangleArea definition and use; received ${JSON.stringify(memberReferences.map(formatLocation))}`
    );
    const moduleUri = vscode.Uri.joinPath(workspaceRoot, 'local', 'geometry.vec').toString();
    assert.ok(memberReferences.some(location => location.uri.toString() === document.uri.toString()));
    assert.ok(memberReferences.some(location => location.uri.toString() === moduleUri));

    const visibleCopiesBefore = visibleEditorCount(document.uri);
    editor.selection = new vscode.Selection(3, memberStart + 1, 3, memberStart + 1);
    await vscode.commands.executeCommand('editor.action.goToReferences');
    await waitUntil(
      () => visibleEditorCount(document.uri) > visibleCopiesBefore,
      'Shift+F12 to open the References peek editor'
    );
    await vscode.commands.executeCommand('closeReferenceSearch');

    const localRename = await getRename(document, new vscode.Position(3, sizeUse + 1), 'sideLength');
    assert.equal(localRename.get(document.uri)?.length, 2);

    const memberRename = await getRename(document, new vscode.Position(3, memberStart + 1), 'calculateArea');
    assert.equal(memberRename.get(document.uri)?.length, 1);
    assert.equal(memberRename.get(vscode.Uri.parse(moduleUri))?.length, 1);

    await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');
    await writeFile(
      resultPath!,
      'PASS: 3 completions, 2 reference searches, Shift+F12 References peek, 2 rename previews, and live diagnostics',
      'utf8'
    );
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

async function getReferences(
  document: vscode.TextDocument,
  position: vscode.Position
): Promise<readonly vscode.Location[]> {
  return await vscode.commands.executeCommand<readonly vscode.Location[]>(
    'vscode.executeReferenceProvider',
    document.uri,
    position
  );
}

function formatLocation(location: vscode.Location): object {
  return {
    uri: location.uri.toString(),
    start: [location.range.start.line, location.range.start.character],
    end: [location.range.end.line, location.range.end.character]
  };
}

function visibleEditorCount(uri: vscode.Uri): number {
  const expected = uri.toString();
  return vscode.window.visibleTextEditors.filter(editor => editor.document.uri.toString() === expected).length;
}

async function getRename(
  document: vscode.TextDocument,
  position: vscode.Position,
  newName: string
): Promise<vscode.WorkspaceEdit> {
  return await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
    'vscode.executeDocumentRenameProvider',
    document.uri,
    position,
    newName
  );
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
