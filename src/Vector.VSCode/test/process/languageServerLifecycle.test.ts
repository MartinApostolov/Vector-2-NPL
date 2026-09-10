import assert from 'node:assert/strict';
import test from 'node:test';
import { LspProcess } from './lspClient';

test('performs a real initialize and graceful shutdown lifecycle', async () => {
  await using server = new LspProcess();

  const result = await server.initialize() as {
    capabilities: Record<string, unknown>;
    serverInfo: { name: string };
  };
  assert.equal(result.serverInfo.name, 'Vector Language Server');
  assert.equal(result.capabilities.hoverProvider, true);
  assert.equal(result.capabilities.definitionProvider, true);
  assert.equal(result.capabilities.referencesProvider, true);
  assert.equal(result.capabilities.renameProvider, true);

  assert.equal(await server.shutdown(), 0);
});
