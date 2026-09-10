import { dirname } from 'node:path';
import { window, type ExtensionContext, type LogOutputChannel } from 'vscode';
import {
  LanguageClient,
  RevealOutputChannelOn,
  TransportKind,
  type LanguageClientOptions,
  type ServerOptions
} from 'vscode-languageclient/node';
import {
  childProcessEnvironment,
  findDotnetRuntime,
  requireFile,
  resolvePayloadPaths
} from './runtime';

export class VectorLanguageServerClient {
  private client: LanguageClient | undefined;

  public constructor(
    private readonly context: ExtensionContext,
    private readonly output: LogOutputChannel
  ) {}

  public async start(): Promise<void> {
    if (this.client?.isRunning() === true) {
      return;
    }

    const paths = resolvePayloadPaths(this.context.extensionPath);
    await requireFile(paths.languageServer, 'The Vector language server');
    const runtime = await findDotnetRuntime();
    this.output.info(`Using dotnet ${runtime.version} for the Vector language server.`);
    this.output.info(`Language server: ${paths.languageServer}`);

    const serverOptions: ServerOptions = {
      command: runtime.command,
      args: [paths.languageServer],
      transport: TransportKind.stdio,
      options: {
        cwd: dirname(paths.languageServer),
        env: childProcessEnvironment(),
        detached: false,
        shell: false
      }
    };
    const clientOptions: LanguageClientOptions = {
      documentSelector: [
        { language: 'vector', scheme: 'file' },
        { language: 'vector', scheme: 'untitled' }
      ],
      diagnosticCollectionName: 'vector',
      outputChannel: this.output,
      revealOutputChannelOn: RevealOutputChannelOn.Never,
      connectionOptions: { maxRestartCount: 3 },
      initializationFailedHandler: error => {
        const message = `Vector language server initialization failed: ${String(error)}`;
        this.output.error(message);
        void window.showErrorMessage(message);
        return false;
      }
    };

    this.client = new LanguageClient(
      'vectorLanguageServer',
      'Vector Language Server',
      serverOptions,
      clientOptions
    );
    await this.client.start();
  }

  public async restart(): Promise<void> {
    if (this.client === undefined) {
      await this.start();
      return;
    }

    await this.client.restart();
  }

  public async stop(): Promise<void> {
    const client = this.client;
    this.client = undefined;
    if (client !== undefined) {
      await client.dispose(5_000);
    }
  }
}
