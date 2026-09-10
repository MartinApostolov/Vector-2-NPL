import { execFile } from 'node:child_process';
import { access } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);

export interface VectorPayloadPaths {
  readonly executionHost: string;
  readonly languageServer: string;
  readonly payloadRoot: string;
}

export interface DotnetRuntimeInfo {
  readonly command: string;
  readonly version: string;
}

export function resolvePayloadPaths(extensionPath: string): VectorPayloadPaths {
  if (extensionPath.trim().length === 0) {
    throw new Error('The Vector extension installation path is unavailable.');
  }

  const payloadRoot = resolve(extensionPath, 'server');
  return {
    payloadRoot,
    languageServer: join(payloadRoot, 'language-server', 'Vector.LanguageServer.dll'),
    executionHost: join(payloadRoot, 'execution-host', 'Vector.ExecutionHost.dll')
  };
}

export async function requireFile(path: string, description: string): Promise<void> {
  try {
    await access(path);
  } catch {
    throw new Error(`${description} was not found at '${path}'. Rebuild or reinstall the Vector extension.`);
  }
}

export function parseNetCoreRuntimeVersion(output: string): string | undefined {
  const versions = output
    .split(/\r?\n/u)
    .map(line => /^Microsoft\.NETCore\.App\s+(\d+)\.(\d+)\.(\d+)\s+/u.exec(line))
    .filter((match): match is RegExpExecArray => match !== null)
    .map(match => ({
      major: Number(match[1]),
      minor: Number(match[2]),
      patch: Number(match[3]),
      version: `${match[1]}.${match[2]}.${match[3]}`
    }))
    .filter(version => version.major === 8)
    .sort((left, right) =>
      right.major - left.major || right.minor - left.minor || right.patch - left.patch);

  return versions[0]?.version;
}

export async function findDotnetRuntime(command = 'dotnet'): Promise<DotnetRuntimeInfo> {
  let stdout: string;
  try {
    ({ stdout } = await execFileAsync(command, ['--list-runtimes'], {
      encoding: 'utf8',
      windowsHide: true
    }));
  } catch (error) {
    const detail = error instanceof Error ? error.message : String(error);
    throw new Error(`The .NET host '${command}' could not be started. Install the .NET 8 runtime and ensure dotnet is on PATH. ${detail}`);
  }

  const version = parseNetCoreRuntimeVersion(stdout);
  if (version === undefined) {
    throw new Error(
      'The Vector VS Code extension requires an installed Microsoft.NETCore.App 8.x runtime. ' +
      'Run \'dotnet --list-runtimes\' to check installed runtimes; .NET 9.x or 10.x alone cannot run the current net8.0 payload.'
    );
  }

  return { command, version };
}

export function childProcessEnvironment(source: NodeJS.ProcessEnv = process.env): NodeJS.ProcessEnv {
  const environment = { ...source };
  // DOTNET_HOST_PATH is an SDK/IDE coordination detail and can point at an IDE-private host.
  // We launch the PATH-resolved dotnet command deliberately and let it select its normal runtime.
  delete environment.DOTNET_HOST_PATH;
  return environment;
}
