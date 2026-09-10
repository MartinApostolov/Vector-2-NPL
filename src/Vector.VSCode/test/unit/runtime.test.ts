import assert from 'node:assert/strict';
import { join, resolve } from 'node:path';
import test from 'node:test';
import {
  childProcessEnvironment,
  findDotnetRuntime,
  parseNetCoreRuntimeVersion,
  resolvePayloadPaths
} from '../../src/runtime';

test('resolves both managed hosts relative to the installed extension', () => {
  const root = resolve('installed-vector-extension');
  const paths = resolvePayloadPaths(root);

  assert.equal(paths.payloadRoot, join(root, 'server'));
  assert.equal(paths.languageServer, join(root, 'server', 'language-server', 'Vector.LanguageServer.dll'));
  assert.equal(paths.executionHost, join(root, 'server', 'execution-host', 'Vector.ExecutionHost.dll'));
});

test('accepts when only Microsoft.NETCore.App 8.x is installed', () => {
  assert.equal(
    parseNetCoreRuntimeVersion('Microsoft.NETCore.App 8.0.20 [C:\\dotnet\\shared\\Microsoft.NETCore.App]'),
    '8.0.20'
  );
});

test('accepts Microsoft.NETCore.App 8.x when newer runtimes are also installed', () => {
  const output = [
    'Microsoft.NETCore.App 8.0.20 [C:\\dotnet\\shared\\Microsoft.NETCore.App]',
    'Microsoft.NETCore.App 9.0.5 [C:\\dotnet\\shared\\Microsoft.NETCore.App]',
    'Microsoft.NETCore.App 10.0.1 [C:\\dotnet\\shared\\Microsoft.NETCore.App]'
  ].join('\r\n');

  assert.equal(parseNetCoreRuntimeVersion(output), '8.0.20');
});

test('rejects when only Microsoft.NETCore.App 9.x is installed', () => {
  assert.equal(
    parseNetCoreRuntimeVersion('Microsoft.NETCore.App 9.0.5 [C:\\dotnet\\shared\\Microsoft.NETCore.App]'),
    undefined
  );
});

test('rejects when only Microsoft.NETCore.App 10.x is installed', () => {
  assert.equal(
    parseNetCoreRuntimeVersion('Microsoft.NETCore.App 10.0.1 [C:\\dotnet\\shared\\Microsoft.NETCore.App]'),
    undefined
  );
});

test('rejects when no compatible Microsoft.NETCore.App runtime is installed', () => {
  assert.equal(parseNetCoreRuntimeVersion('Microsoft.NETCore.App 7.0.20 [C:\\dotnet]'), undefined);
  assert.equal(parseNetCoreRuntimeVersion(''), undefined);
});

test('does not mistake Microsoft.AspNetCore.App for the required runtime', () => {
  const output = [
    'Microsoft.AspNetCore.App 8.0.20 [C:\\dotnet\\shared\\Microsoft.AspNetCore.App]',
    'Microsoft.AspNetCore.App 10.0.1 [C:\\dotnet\\shared\\Microsoft.AspNetCore.App]'
  ].join('\r\n');

  assert.equal(parseNetCoreRuntimeVersion(output), undefined);
});

test('uses a real compatible dotnet runtime', async () => {
  const runtime = await findDotnetRuntime();
  assert.equal(runtime.command, 'dotnet');
  assert.match(runtime.version, /^8\.\d+\.\d+$/u);
});

test('does not inherit an IDE-private DOTNET_HOST_PATH', () => {
  const environment = childProcessEnvironment({
    DOTNET_HOST_PATH: 'C:\\ide-private\\dotnet.exe',
    DOTNET_ROOT: 'C:\\dotnet',
    VECTOR_TEST: 'preserved'
  });

  assert.equal(environment.DOTNET_HOST_PATH, undefined);
  assert.equal(environment.DOTNET_ROOT, 'C:\\dotnet');
  assert.equal(environment.VECTOR_TEST, 'preserved');
});
