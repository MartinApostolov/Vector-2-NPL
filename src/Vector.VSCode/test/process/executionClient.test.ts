import assert from 'node:assert/strict';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { VectorExecutionClient } from '../../src/executionClient';
import type { VectorExecutionEngine, VectorExecutionRequest } from '../../src/executionProtocol';

function builtExecutionHostPath(): string {
  const extensionRoot = join(__dirname, '..', '..', '..');
  return join(extensionRoot, '..', 'Vector.ExecutionHost', 'bin', 'Release', 'net8.0', 'Vector.ExecutionHost.dll');
}

function request(
  source: string,
  sourcePath: string,
  programRoot: string,
  engine: VectorExecutionEngine,
  operation: 'run' | 'disassemble' = 'run'
): VectorExecutionRequest {
  return { source, sourcePath, programRoot, engine, operation, pluginPaths: [] };
}

test('runs unsaved local-module source through Interpreter and VM and cleans up hosts', async () => {
  const root = await mkdtemp(join(tmpdir(), 'vector-vscode-execution-'));
  try {
    await mkdir(join(root, 'local'));
    await writeFile(
      join(root, 'local', 'geometry.vec'),
      'function rectangleArea(width, height) { return width * height; }\nlet origin = [0, 0];',
      'utf8'
    );
    const sourcePath = join(root, 'main.vec');
    await writeFile(sourcePath, 'let size = 6;\nsize;', 'utf8');
    const unsavedSource = [
      'import local.geometry;',
      'let size = 8;',
      'let area = local.geometry.rectangleArea(size, 7);',
      'print("Vector VS Code acceptance");',
      'print(area);',
      'area;'
    ].join('\n');
    const client = new VectorExecutionClient(builtExecutionHostPath());

    for (const engine of ['interpreter', 'vm'] as const) {
      const response = await client.execute(request(unsavedSource, sourcePath, root, engine));
      assert.equal(response.success, true);
      assert.deepEqual(response.output, ['Vector VS Code acceptance', '56']);
      assert.equal(response.result, '56');
      assert.equal(client.activeHostCount, 0);
    }

    client.dispose();
    assert.equal(client.activeHostCount, 0);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test('disassembles through the VM pipeline without executing print', async () => {
  const root = await mkdtemp(join(tmpdir(), 'vector-vscode-bytecode-'));
  try {
    const sourcePath = join(root, 'main.vec');
    const client = new VectorExecutionClient(builtExecutionHostPath());
    const response = await client.execute(request(
      'print("must not run");\n42;',
      sourcePath,
      root,
      'vm',
      'disassemble'
    ));

    assert.equal(response.success, true);
    assert.deepEqual(response.output, []);
    assert.equal(response.result, null);
    assert.match(response.disassembly ?? '', /Constant/u);
    assert.match(response.disassembly ?? '', /Call/u);
    assert.equal(client.activeHostCount, 0);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test('returns structured host failures and reports missing payloads', async () => {
  const root = await mkdtemp(join(tmpdir(), 'vector-vscode-errors-'));
  try {
    const client = new VectorExecutionClient(builtExecutionHostPath());
    const missingRoot = join(root, 'missing');
    const response = await client.execute(request('1;', join(root, 'main.vec'), missingRoot, 'interpreter'));
    assert.equal(response.success, false);
    assert.equal(response.hostFailure?.code, 'invalid_request');
    assert.equal(client.activeHostCount, 0);

    const missingClient = new VectorExecutionClient(join(root, 'missing-host.dll'));
    await assert.rejects(
      missingClient.execute(request('1;', join(root, 'main.vec'), root, 'interpreter')),
      /was not found/u
    );
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
