import { spawn } from 'node:child_process';

const [
  vscodeExecutablePath,
  extensionsDirectory,
  userDataDirectory,
  workspacePath
] = process.argv.slice(2);

if ([
  vscodeExecutablePath,
  extensionsDirectory,
  userDataDirectory,
  workspacePath
].some(value => value === undefined)) {
  throw new Error('Installed VSIX test driver requires four path arguments.');
}

const exitCode = await new Promise((resolve, reject) => {
  const child = spawn(vscodeExecutablePath, [
    workspacePath,
    `--extensions-dir=${extensionsDirectory}`,
    `--user-data-dir=${userDataDirectory}`,
    '--no-sandbox',
    '--disable-gpu-sandbox',
    '--disable-updates',
    '--skip-welcome',
    '--skip-release-notes',
    '--disable-workspace-trust'
  ], {
    stdio: 'inherit',
    windowsHide: true
  });
  child.once('error', reject);
  child.once('exit', code => resolve(code));
});

if (exitCode !== 0) {
  throw new Error(`Installed VSIX test instance exited with code ${exitCode}.`);
}
