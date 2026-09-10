import { defineConfig } from '@vscode/test-cli';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const extensionRoot = dirname(fileURLToPath(import.meta.url));
const configuredExecutable = process.env.VECTOR_VSCODE_EXECUTABLE;

export default defineConfig({
  files: 'dist-test/test/suite/*.test.js',
  extensionDevelopmentPath: extensionRoot,
  workspaceFolder: join(extensionRoot, 'test', 'fixtures', 'workspace'),
  launchArgs: [
    '--disable-extensions',
    '--disable-workspace-trust',
    '--skip-release-notes',
    '--skip-welcome'
  ],
  ...(configuredExecutable === undefined
    ? {}
    : { useInstallation: { fromPath: configuredExecutable } }),
  mocha: {
    timeout: 20_000
  }
});
