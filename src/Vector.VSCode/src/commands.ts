import { commands, ProgressLocation, window, type LogOutputChannel } from 'vscode';
import { VectorExecutionClient } from './executionClient';
import type {
  VectorExecutionEngine,
  VectorExecutionOperation,
  VectorExecutionRequest,
  VectorExecutionResponse
} from './executionProtocol';
import { defaultProgramRoot, getExecutionSettings, sourcePath } from './settings';

export const commandIds = {
  run: 'vector.runCurrentFile',
  runInterpreter: 'vector.runCurrentFileWithInterpreter',
  runVm: 'vector.runCurrentFileWithVm',
  showBytecode: 'vector.showBytecode'
} as const;

export function registerVectorCommands(
  client: VectorExecutionClient,
  output: LogOutputChannel
): { dispose(): void }[] {
  return [
    commands.registerCommand(commandIds.run, () => executeCurrentDocument(client, output)),
    commands.registerCommand(commandIds.runInterpreter, () => executeCurrentDocument(client, output, 'interpreter')),
    commands.registerCommand(commandIds.runVm, () => executeCurrentDocument(client, output, 'vm')),
    commands.registerCommand(commandIds.showBytecode, () => executeCurrentDocument(client, output, 'vm', 'disassemble'))
  ];
}

async function executeCurrentDocument(
  client: VectorExecutionClient,
  output: LogOutputChannel,
  engineOverride?: VectorExecutionEngine,
  operation: VectorExecutionOperation = 'run'
): Promise<void> {
  const editor = window.activeTextEditor;
  if (editor === undefined || editor.document.languageId !== 'vector') {
    const message = 'Open an active Vector (.vec) editor before running this command.';
    output.error(message);
    output.show(true);
    await window.showErrorMessage(message);
    return;
  }

  const document = editor.document;
  const settings = getExecutionSettings(document);
  const engine = engineOverride ?? settings.defaultEngine;
  const path = sourcePath(document);
  const request: VectorExecutionRequest = {
    source: document.getText(),
    ...(path === undefined ? {} : { sourcePath: path }),
    programRoot: settings.programRoot ?? defaultProgramRoot(document),
    engine,
    operation,
    pluginPaths: operation === 'disassemble' ? [] : settings.pluginPaths
  };

  output.clear();
  output.appendLine(operation === 'disassemble' ? 'Vector bytecode' : 'Vector execution');
  output.appendLine(`Engine: ${displayEngine(engine)}`);
  output.appendLine(`Source: ${path ?? document.uri.toString()}`);
  if (request.programRoot !== undefined) {
    output.appendLine(`Program root: ${request.programRoot}`);
  }
  output.appendLine('');
  output.show(true);

  try {
    const response = await window.withProgress({
      location: ProgressLocation.Notification,
      title: operation === 'disassemble' ? 'Compiling Vector bytecode' : `Running Vector with ${displayEngine(engine)}`,
      cancellable: true
    }, async (_progress, token) => {
      const cancellation = new AbortController();
      const subscription = token.onCancellationRequested(() => cancellation.abort(new Error('Vector execution was cancelled.')));
      try {
        return await client.execute(request, cancellation.signal);
      } finally {
        subscription.dispose();
      }
    });
    writeResponse(output, response);
    if (!response.success) {
      await window.showErrorMessage(failureSummary(response));
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    output.appendLine(`Execution error: ${message}`);
    await window.showErrorMessage(`Vector command failed: ${message}`);
  }
}

function writeResponse(output: LogOutputChannel, response: VectorExecutionResponse): void {
  if (response.operation === 'disassemble') {
    output.appendLine(response.disassembly ?? '(No disassembly was produced.)');
  } else {
    output.appendLine('Program output:');
    if (response.output.length === 0) {
      output.appendLine('(none)');
    } else {
      response.output.forEach(line => output.appendLine(line));
    }
    output.appendLine('');
    output.appendLine(`Final result: ${response.result ?? 'nothing'}`);
  }

  if (response.diagnostics.length > 0) {
    output.appendLine('');
    output.appendLine('Diagnostics:');
    for (const diagnostic of response.diagnostics) {
      const location = diagnostic.sourceName == null
        ? ''
        : `${diagnostic.sourceName}:${diagnostic.range.start.line + 1}:${diagnostic.range.start.character + 1}: `;
      output.appendLine(`${location}${diagnostic.severity} ${diagnostic.code}: ${diagnostic.message}`);
    }
  }
  if (response.hostFailure != null) {
    output.appendLine('');
    output.appendLine(`Execution host error ${response.hostFailure.code}: ${response.hostFailure.message}`);
  }
}

function failureSummary(response: VectorExecutionResponse): string {
  if (response.hostFailure != null) {
    return `Vector command failed: ${response.hostFailure.message}`;
  }
  return `Vector command failed with ${response.diagnostics.length} diagnostic(s). See the Vector output channel.`;
}

function displayEngine(engine: VectorExecutionEngine): string {
  return engine === 'vm' ? 'VM' : 'Interpreter';
}
