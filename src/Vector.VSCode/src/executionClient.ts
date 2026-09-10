import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { dirname } from 'node:path';
import type { VectorExecutionRequest, VectorExecutionResponse } from './executionProtocol';
import { childProcessEnvironment, findDotnetRuntime, requireFile } from './runtime';

export interface ExecutionClientOptions {
  readonly dotnetCommand?: string;
  readonly timeoutMilliseconds?: number;
}

export class VectorExecutionClient {
  private readonly activeHosts = new Set<ChildProcessWithoutNullStreams>();
  private readonly timeoutMilliseconds: number;

  public constructor(
    private readonly hostPath: string,
    options: ExecutionClientOptions = {}
  ) {
    this.timeoutMilliseconds = options.timeoutMilliseconds ?? 30_000;
    if (this.timeoutMilliseconds <= 0) {
      throw new Error('The Vector execution timeout must be positive.');
    }
    this.dotnetCommand = options.dotnetCommand;
  }

  private readonly dotnetCommand: string | undefined;

  public get activeHostCount(): number {
    return this.activeHosts.size;
  }

  public async execute(request: VectorExecutionRequest, signal?: AbortSignal): Promise<VectorExecutionResponse> {
    await requireFile(this.hostPath, 'The Vector execution host');
    const runtime = await findDotnetRuntime(this.dotnetCommand);
    const child = spawn(runtime.command, [this.hostPath], {
      cwd: dirname(this.hostPath),
      env: childProcessEnvironment(),
      stdio: 'pipe',
      windowsHide: true,
      detached: false
    });
    this.activeHosts.add(child);

    try {
      return await this.exchange(child, request, signal);
    } finally {
      this.kill(child);
      this.activeHosts.delete(child);
    }
  }

  public dispose(): void {
    for (const child of this.activeHosts) {
      this.kill(child);
    }
    this.activeHosts.clear();
  }

  private async exchange(
    child: ChildProcessWithoutNullStreams,
    request: VectorExecutionRequest,
    signal?: AbortSignal
  ): Promise<VectorExecutionResponse> {
    let output = '';
    let standardError = '';
    child.stdout.setEncoding('utf8');
    child.stderr.setEncoding('utf8');
    child.stdout.on('data', (chunk: string) => { output += chunk; });
    child.stderr.on('data', (chunk: string) => { standardError += chunk; });

    const exit = new Promise<{ code: number | null; signal: NodeJS.Signals | null }>((resolve, reject) => {
      child.once('error', reject);
      child.once('exit', (code, childSignal) => resolve({ code, signal: childSignal }));
    });
    const timeout = AbortSignal.timeout(this.timeoutMilliseconds);
    const combined = signal === undefined ? timeout : AbortSignal.any([signal, timeout]);
    const aborted = new Promise<never>((_resolve, reject) => {
      combined.addEventListener('abort', () => {
        reject(combined.reason instanceof Error ? combined.reason : new Error('Vector execution was cancelled.'));
      }, { once: true });
    });

    child.stdin.end(JSON.stringify(request), 'utf8');

    let outcome: { code: number | null; signal: NodeJS.Signals | null };
    try {
      outcome = await Promise.race([exit, aborted]);
    } catch (error) {
      this.kill(child);
      if (timeout.aborted && signal?.aborted !== true) {
        throw new Error(`Vector execution exceeded ${this.timeoutMilliseconds} ms.`);
      }
      throw error;
    }

    const trimmedOutput = output.trim();
    if (trimmedOutput.length === 0) {
      const detail = standardError.trim() || `exit code ${String(outcome.code)}, signal ${String(outcome.signal)}`;
      throw new Error(`Vector execution host returned no response (${detail}).`);
    }

    let response: VectorExecutionResponse;
    try {
      response = JSON.parse(trimmedOutput) as VectorExecutionResponse;
    } catch (error) {
      throw new Error(`Vector execution host returned invalid JSON: ${error instanceof Error ? error.message : String(error)}. stderr: ${standardError.trim()}`);
    }

    if (outcome.code !== 0 && response.hostFailure == null) {
      throw new Error(`Vector execution host exited with code ${String(outcome.code)}. ${standardError.trim()}`.trim());
    }
    return response;
  }

  private kill(child: ChildProcessWithoutNullStreams): void {
    if (child.exitCode === null && child.signalCode === null) {
      child.kill();
    }
  }
}
