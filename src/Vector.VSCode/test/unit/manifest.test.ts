import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import test from 'node:test';

interface ExtensionManifest {
  activationEvents: string[];
  contributes: {
    languages: Array<{
      configuration: string;
      id: string;
      extensions: string[];
    }>;
    grammars: Array<{
      language: string;
      path: string;
      scopeName: string;
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
  assert.equal(vector.configuration, './language-configuration.json');

  assert.deepEqual(manifest.contributes.grammars, [{
    language: 'vector',
    path: './syntaxes/vector.tmLanguage.json',
    scopeName: 'source.vector'
  }]);
});

test('packages the tested Visual Studio grammar without divergence', async () => {
  const extensionRoot = join(__dirname, '..', '..', '..');
  const repositoryRoot = join(extensionRoot, '..', '..');
  const vscodeGrammar = await readFile(join(extensionRoot, 'syntaxes', 'vector.tmLanguage.json'), 'utf8');
  const visualStudioGrammar = await readFile(
    join(repositoryRoot, 'src', 'Vector.VisualStudio', 'Grammars', 'vector.tmLanguage.json'),
    'utf8'
  );
  assert.deepEqual(JSON.parse(vscodeGrammar), JSON.parse(visualStudioGrammar));
});

test('keeps editor behavior synchronized and covers all requested pairs', async () => {
  const extensionRoot = join(__dirname, '..', '..', '..');
  const repositoryRoot = join(extensionRoot, '..', '..');
  const vscodeConfiguration = await readFile(join(extensionRoot, 'language-configuration.json'), 'utf8');
  const visualStudioConfiguration = await readFile(
    join(repositoryRoot, 'src', 'Vector.VisualStudio', 'LanguageConfiguration', 'vector-language-configuration.json'),
    'utf8'
  );
  assert.deepEqual(JSON.parse(vscodeConfiguration), JSON.parse(visualStudioConfiguration));

  const configuration = JSON.parse(vscodeConfiguration) as {
    autoClosingPairs: Array<{ open: string; close: string }>;
    brackets: string[][];
    comments: { blockComment: string[]; lineComment: string };
    indentationRules: object;
  };
  assert.equal(configuration.comments.lineComment, '//');
  assert.deepEqual(configuration.comments.blockComment, ['/*', '*/']);
  assert.deepEqual(configuration.brackets, [['{', '}'], ['[', ']'], ['(', ')']]);
  assert.deepEqual(
    configuration.autoClosingPairs.map(pair => [pair.open, pair.close]),
    [['{', '}'], ['[', ']'], ['(', ')'], ['"', '"'], ["'", "'"]]
  );
  assert.ok(configuration.indentationRules);
});
