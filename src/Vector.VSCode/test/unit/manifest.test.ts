import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import test from 'node:test';

interface ExtensionManifest {
  activationEvents: string[];
  contributes: {
    languages: Array<{
      id: string;
      extensions: string[];
    }>;
  };
  main: string;
}

test('registers .vec files as the Vector language', async () => {
  const manifestPath = join(__dirname, '..', '..', '..', 'package.json');
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8')) as ExtensionManifest;

  assert.equal(manifest.main, './dist/extension.js');
  assert.ok(manifest.activationEvents.includes('onLanguage:vector'));
  const vector = manifest.contributes.languages.find(language => language.id === 'vector');
  assert.ok(vector);
  assert.deepEqual(vector.extensions, ['.vec']);
});
