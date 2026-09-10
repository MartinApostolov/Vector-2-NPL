import assert from 'node:assert/strict';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import test from 'node:test';
import { LspProcess } from './lspClient';

interface PublishedDiagnostics {
  readonly diagnostics: Array<{ readonly code: string; readonly message: string; readonly severity: number }>;
  readonly uri: string;
}

test('publishes and clears parser diagnostics from full in-memory document changes', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'vector-vscode-diagnostics-'));
  try {
    const path = join(directory, 'malformed.vec');
    await writeFile(path, 'let missingExpression = 99;', 'utf8');
    const uri = pathToFileURL(path).href;
    await using server = new LspProcess();
    await server.initialize(pathToFileURL(directory).href);

    server.notify('textDocument/didOpen', {
      textDocument: {
        uri,
        languageId: 'vector',
        version: 1,
        text: 'let missingExpression = ;'
      }
    });
    const malformed = await server.waitForNotification('textDocument/publishDiagnostics') as PublishedDiagnostics;
    assert.equal(malformed.uri, uri);
    assert.ok(malformed.diagnostics.some(diagnostic => diagnostic.severity === 1));

    server.notify('textDocument/didChange', {
      textDocument: { uri, version: 2 },
      contentChanges: [{ text: 'let missingExpression = 1;' }]
    });
    const fixed = await server.waitForNotification('textDocument/publishDiagnostics') as PublishedDiagnostics;
    assert.equal(fixed.uri, uri);
    assert.deepEqual(fixed.diagnostics, []);

    server.notify('textDocument/didClose', { textDocument: { uri } });
    const closed = await server.waitForNotification('textDocument/publishDiagnostics') as PublishedDiagnostics;
    assert.deepEqual(closed.diagnostics, []);
    assert.equal(await server.shutdown(), 0);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});
