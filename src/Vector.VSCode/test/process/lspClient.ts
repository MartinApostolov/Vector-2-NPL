import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { dirname, join } from 'node:path';

export interface JsonRpcMessage {
  readonly error?: { readonly code: number; readonly message: string };
  readonly id?: number;
  readonly method?: string;
  readonly params?: unknown;
  readonly result?: unknown;
}

export function builtLanguageServerPath(): string {
  const extensionRoot = join(__dirname, '..', '..', '..');
  return join(extensionRoot, '..', 'Vector.LanguageServer', 'bin', 'Release', 'net8.0', 'Vector.LanguageServer.dll');
}

export class LspProcess implements AsyncDisposable {
  private buffer = Buffer.alloc(0);
  private readonly messages: JsonRpcMessage[] = [];
  private readonly waiters: Array<() => void> = [];
  private nextId = 1;
  private standardError = '';
  public readonly process: ChildProcessWithoutNullStreams;

  public constructor(serverPath = builtLanguageServerPath(), environment: NodeJS.ProcessEnv = process.env) {
    this.process = spawn('dotnet', [serverPath], {
      cwd: dirname(serverPath),
      env: { ...environment, DOTNET_HOST_PATH: undefined },
      stdio: 'pipe',
      windowsHide: true
    });
    this.process.stdout.on('data', (chunk: Buffer) => this.receive(chunk));
    this.process.stderr.setEncoding('utf8');
    this.process.stderr.on('data', (chunk: string) => { this.standardError += chunk; });
  }

  public async initialize(rootUri: string | null = null): Promise<Record<string, unknown>> {
    const result = await this.request('initialize', {
      processId: process.pid,
      rootUri,
      capabilities: {}
    }) as Record<string, unknown>;
    this.notify('initialized', {});
    return result;
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

  public waitForNotification(method: string): Promise<unknown> {
    return this.waitFor(message => message.method === method).then(message => message.params);
  }

  public async shutdown(): Promise<number | null> {
    await this.request('shutdown');
    this.notify('exit');
    return this.waitForExit();
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
      this.messages.push(JSON.parse(body) as JsonRpcMessage);
      this.waiters.splice(0).forEach(waiter => waiter());
    }
  }

  private async waitFor(predicate: (message: JsonRpcMessage) => boolean): Promise<JsonRpcMessage> {
    const deadline = Date.now() + 5_000;
    for (;;) {
      const index = this.messages.findIndex(predicate);
      if (index >= 0) {
        return this.messages.splice(index, 1)[0] as JsonRpcMessage;
      }
      const remaining = deadline - Date.now();
      if (remaining <= 0) {
        throw new Error(`Timed out waiting for LSP message. stderr: ${this.standardError}`);
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
