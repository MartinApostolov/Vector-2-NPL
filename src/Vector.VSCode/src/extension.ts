import { window, type ExtensionContext, type LogOutputChannel } from 'vscode';
import { VectorLanguageServerClient } from './languageServer';

let languageServer: VectorLanguageServerClient | undefined;
let output: LogOutputChannel | undefined;

export async function activate(context: ExtensionContext): Promise<void> {
  output = window.createOutputChannel('Vector', { log: true });
  context.subscriptions.push(output);
  languageServer = new VectorLanguageServerClient(context, output);

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
  output = undefined;
}
