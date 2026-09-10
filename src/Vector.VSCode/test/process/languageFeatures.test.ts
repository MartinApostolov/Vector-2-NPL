import assert from 'node:assert/strict';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import test from 'node:test';
import { LspProcess } from './lspClient';

interface CompletionItem { readonly label: string }
interface Location { readonly uri: string; readonly range: { readonly start: { readonly line: number; readonly character: number } } }
interface WorkspaceEdit { readonly changes: Record<string, Array<{ readonly newText: string }>> }

const mainSource = [
  'import local.geometry;',
  '',
  'let size = 6;',
  'let area = local.geometry.rectangleArea(size, 7);',
  'print("Vector VS Code acceptance");',
  'print(area);',
  'area;',
  'pri',
  'local.geometry.'
].join('\n');

const moduleSource = [
  'function rectangleArea(width, height) {',
  '    return width * height;',
  '}',
  '',
  'let origin = [0, 0];'
].join('\n');

function position(line: number, character: number): { line: number; character: number } {
  return { line, character };
}

function documentPosition(uri: string, line: number, character: number): object {
  return { textDocument: { uri }, position: position(line, character) };
}

test('exposes shared IntelliSense, navigation, references, and rename over real LSP', async () => {
  const root = await mkdtemp(join(tmpdir(), 'vector-vscode-features-'));
  try {
    const local = join(root, 'local');
    await mkdir(local);
    const mainPath = join(root, 'main.vec');
    const modulePath = join(local, 'geometry.vec');
    await writeFile(mainPath, mainSource, 'utf8');
    await writeFile(modulePath, moduleSource, 'utf8');
    const mainUri = pathToFileURL(mainPath).href;
    const moduleUri = pathToFileURL(modulePath).href;

    await using server = new LspProcess();
    await server.initialize(pathToFileURL(root).href);
    server.notify('textDocument/didOpen', {
      textDocument: { uri: moduleUri, languageId: 'vector', version: 1, text: moduleSource }
    });
    server.notify('textDocument/didOpen', {
      textDocument: { uri: mainUri, languageId: 'vector', version: 1, text: mainSource }
    });

    const prefixCompletion = await server.request(
      'textDocument/completion',
      documentPosition(mainUri, 7, 3)
    ) as CompletionItem[];
    assert.ok(prefixCompletion.some(item => item.label === 'print'));

    const moduleCompletion = await server.request(
      'textDocument/completion',
      documentPosition(mainUri, 8, 'local.geometry.'.length)
    ) as CompletionItem[];
    assert.deepEqual(
      moduleCompletion.map(item => item.label).sort(),
      ['origin', 'rectangleArea']
    );

    const builtinHover = await server.request(
      'textDocument/hover',
      documentPosition(mainUri, 4, 2)
    ) as { contents: { value: string } };
    assert.match(builtinHover.contents.value, /print\(value\)/u);

    const localHover = await server.request(
      'textDocument/hover',
      documentPosition(mainUri, 5, 7)
    ) as { contents: { value: string } };
    assert.match(localHover.contents.value, /area/u);

    const memberStart = mainSource.split('\n')[3]?.indexOf('rectangleArea') ?? -1;
    assert.ok(memberStart >= 0);
    const moduleHover = await server.request(
      'textDocument/hover',
      documentPosition(mainUri, 3, memberStart + 2)
    ) as { contents: { value: string } };
    assert.match(moduleHover.contents.value, /rectangleArea\(width, height\)/u);

    const sizeUse = mainSource.split('\n')[3]?.indexOf('size') ?? -1;
    const localDefinition = await server.request(
      'textDocument/definition',
      documentPosition(mainUri, 3, sizeUse + 1)
    ) as Location;
    assert.equal(localDefinition.uri, mainUri);
    assert.deepEqual(localDefinition.range.start, position(2, 4));

    const moduleDefinition = await server.request(
      'textDocument/definition',
      documentPosition(mainUri, 3, memberStart + 2)
    ) as Location;
    assert.equal(moduleDefinition.uri, moduleUri);
    assert.deepEqual(moduleDefinition.range.start, position(0, 'function '.length));

    const builtinSignature = await server.request(
      'textDocument/signatureHelp',
      documentPosition(mainUri, 4, 'print('.length)
    ) as { signatures: Array<{ label: string }> };
    assert.equal(builtinSignature.signatures[0]?.label, 'print(value)');

    const userSignature = await server.request(
      'textDocument/signatureHelp',
      documentPosition(mainUri, 3, memberStart + 'rectangleArea('.length)
    ) as { signatures: Array<{ label: string }> };
    assert.equal(userSignature.signatures[0]?.label, 'local.geometry.rectangleArea(width, height)');

    const localReferences = await server.request('textDocument/references', {
      ...documentPosition(mainUri, 3, sizeUse + 1),
      context: { includeDeclaration: true }
    }) as Location[];
    assert.equal(localReferences.length, 2);

    const moduleReferences = await server.request('textDocument/references', {
      ...documentPosition(mainUri, 3, memberStart + 2),
      context: { includeDeclaration: true }
    }) as Location[];
    assert.equal(moduleReferences.length, 2);
    assert.ok(moduleReferences.some(location => location.uri === moduleUri));
    assert.ok(moduleReferences.some(location => location.uri === mainUri));

    const localRename = await server.request('textDocument/rename', {
      ...documentPosition(mainUri, 3, sizeUse + 1),
      newName: 'widthValue'
    }) as WorkspaceEdit;
    assert.equal(localRename.changes[mainUri]?.length, 2);
    assert.ok(localRename.changes[mainUri]?.every(edit => edit.newText === 'widthValue'));

    const moduleRename = await server.request('textDocument/rename', {
      ...documentPosition(mainUri, 3, memberStart + 2),
      newName: 'areaOfRectangle'
    }) as WorkspaceEdit;
    assert.equal(moduleRename.changes[moduleUri]?.length, 1);
    assert.equal(moduleRename.changes[mainUri]?.length, 1);
    assert.equal(moduleRename.changes[moduleUri]?.[0]?.newText, 'areaOfRectangle');
    assert.equal(moduleRename.changes[mainUri]?.[0]?.newText, 'areaOfRectangle');

    assert.equal(await server.shutdown(), 0);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test('keeps function parameters inside their lexical scope', async () => {
  const source = [
    'function calculate(width) {',
    '    wid',
    '}',
    'wid'
  ].join('\n');
  const uri = 'untitled:vector-lexical-scope.vec';
  await using server = new LspProcess();
  await server.initialize();
  server.notify('textDocument/didOpen', {
    textDocument: { uri, languageId: 'vector', version: 1, text: source }
  });

  const inside = await server.request('textDocument/completion', documentPosition(uri, 1, 7)) as CompletionItem[];
  const outside = await server.request('textDocument/completion', documentPosition(uri, 3, 3)) as CompletionItem[];
  assert.ok(inside.some(item => item.label === 'width'));
  assert.ok(!outside.some(item => item.label === 'width'));
  assert.equal(await server.shutdown(), 0);
});
