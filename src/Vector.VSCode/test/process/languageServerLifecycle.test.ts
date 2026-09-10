import assert from 'node:assert/strict';
import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { join } from 'node:path';
import test from 'node:test';

interface JsonRpcResponse {
  readonly error?: { readonly code: number; readonly message: string };
  readonly id?: number;
  readonly result?: unknown;
}

class LspProcess implements AsyncDisposable {
  private buffer = Buffer.alloc(0);
  private readonly messages: JsonRpcResponse[] = [];
  private readonly waiters: Array<() => void> = [];
  private nextId = 1;
  private standardError = '';
  public readonly process: ChildProcessWithoutNullStreams;

  public constructor(serverPath: string) {
    this.process = spawn('dotnet', [serverPath], {
      cwd: join(serverPath, '..'),
      env: { ...process.env, DOTNET_HOST_PATH: undefined },
      stdio: 'pipe',
      windowsHide: true
    });
    this.process.stdout.on('data', (chunk: Buffer) => this.receive(chunk));
    this.process.stderr.setEncoding('utf8');
    this.process.stderr.on('data', (chunk: string) => { this.standardError += chunk; });
  }

  public async request(method: string, params?: unknown): Promise<unknown> {
    const id = this.nextId++;
    this.send({ jsonrpc: '2.0', id, method, ...(params === undefined ? {} : { params }) });
    const response = await this.waitFor(message => message.id === id);
    if (response.error !== undefined) {
      throw new Error(`${response.error.code}: ${response.error.message}`);
    }
    return response.result;
  }

  public notify(method: string, params?: unknown): void {
    this.send({ jsonrpc: '2.0', method, ...(params === undefined ? {} : { params }) });
  }

  public async [Symbol.asyncDispose](): Promise<void> {
    if (this.process.exitCode === null) {
      this.process.kill();
      await new Promise(resolve => this.process.once('exit', resolve));
    }
  }

  public async waitForExit(): Promise<number | null> {
    if (this.process.exitCode !== null) {
      return this.process.exitCode;
    }
    return await new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error(`Language server did not exit. stderr: ${this.standardError}`)), 5_000);
      this.process.once('exit', code => {
        clearTimeout(timer);
        resolve(code);
      });
    });
  }

  private send(message: unknown): void {
    const body = Buffer.from(JSON.stringify(message), 'utf8');
    this.process.stdin.write(`Content-Length: ${body.length}\r\n\r\n`);
    this.process.stdin.write(body);
  }

  private receive(chunk: Buffer): void {
    this.buffer = Buffer.concat([this.buffer, chunk]);
    for (;;) {
      const headerEnd = this.buffer.indexOf('\r\n\r\n');
      if (headerEnd < 0) {
        return;
      }
      const header = this.buffer.subarray(0, headerEnd).toString('ascii');
      const length = /Content-Length:\s*(\d+)/iu.exec(header);
      if (length?.[1] === undefined) {
        throw new Error(`Invalid LSP header: ${header}`);
      }
      const bodyLength = Number(length[1]);
      const bodyStart = headerEnd + 4;
      if (this.buffer.length < bodyStart + bodyLength) {
        return;
      }
      const body = this.buffer.subarray(bodyStart, bodyStart + bodyLength).toString('utf8');
      this.buffer = this.buffer.subarray(bodyStart + bodyLength);
      this.messages.push(JSON.parse(body) as JsonRpcResponse);
      this.waiters.splice(0).forEach(waiter => waiter());
    }
  }

  private async waitFor(predicate: (message: JsonRpcResponse) => boolean): Promise<JsonRpcResponse> {
    const deadline = Date.now() + 5_000;
    for (;;) {
      const index = this.messages.findIndex(predicate);
      if (index >= 0) {
        return this.messages.splice(index, 1)[0] as JsonRpcResponse;
      }
      const remaining = deadline - Date.now();
      if (remaining <= 0) {
        throw new Error(`Timed out waiting for LSP response. stderr: ${this.standardError}`);
      }
      await new Promise<void>(resolve => {
        const timer = setTimeout(resolve, remaining);
        this.waiters.push(() => {
          clearTimeout(timer);
          resolve();
        });
      });
    }
  }
}

test('performs a real initialize and graceful shutdown lifecycle', async () => {
  const extensionRoot = join(__dirname, '..', '..', '..');
  const serverPath = join(extensionRoot, '..', 'Vector.LanguageServer', 'bin', 'Release', 'net8.0', 'Vector.LanguageServer.dll');
  await using server = new LspProcess(serverPath);

  const result = await server.request('initialize', {
    processId: process.pid,
    rootUri: null,
    capabilities: {}
  }) as { capabilities: Record<string, unknown>; serverInfo: { name: string } };
  assert.equal(result.serverInfo.name, 'Vector Language Server');
  assert.equal(result.capabilities.hoverProvider, true);
  assert.equal(result.capabilities.definitionProvider, true);
  assert.equal(result.capabilities.referencesProvider, true);
  assert.equal(result.capabilities.renameProvider, true);

  server.notify('initialized', {});
  assert.equal(await server.request('shutdown'), null);
  server.notify('exit');
  assert.equal(await server.waitForExit(), 0);
});
