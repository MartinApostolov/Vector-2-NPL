import { window, workspace, type ExtensionContext, type LogOutputChannel } from 'vscode';
import { registerVectorCommands } from './commands';
import { VectorExecutionClient } from './executionClient';
import { VectorLanguageServerClient } from './languageServer';
import { resolvePayloadPaths } from './runtime';

let languageServer: VectorLanguageServerClient | undefined;
let output: LogOutputChannel | undefined;
let executionClient: VectorExecutionClient | undefined;

export async function activate(context: ExtensionContext): Promise<void> {
  output = window.createOutputChannel('Vector', { log: true });
  context.subscriptions.push(output);
  const paths = resolvePayloadPaths(context.extensionPath);
  executionClient = new VectorExecutionClient(paths.executionHost);
  context.subscriptions.push(executionClient, ...registerVectorCommands(executionClient, output));
  languageServer = new VectorLanguageServerClient(context, output);
  context.subscriptions.push(workspace.onDidChangeConfiguration(event => {
    if (event.affectsConfiguration('vector.liveDiagnostics') || event.affectsConfiguration('vector.programRoot')) {
      void languageServer?.restartIfConfigurationChanged().catch(error => {
        const message = `Vector language server reconfiguration failed: ${error instanceof Error ? error.message : String(error)}`;
        output?.error(message);
        void window.showErrorMessage(message);
      });
    }
  }));

  try {
    await languageServer.start();
  } catch (error) {
    const message = `Vector language server failed to start: ${error instanceof Error ? error.message : String(error)}`;
    output.error(message);
    void window.showErrorMessage(message, 'Show Output').then(selection => {
      if (selection === 'Show Output') {
        output?.show(true);
      }
    });
  }
}

export async function deactivate(): Promise<void> {
  await languageServer?.stop();
  languageServer = undefined;
  executionClient?.dispose();
  executionClient = undefined;
  output = undefined;
}
