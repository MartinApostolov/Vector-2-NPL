import { dirname, isAbsolute, resolve } from 'node:path';
import { Uri, workspace, type TextDocument } from 'vscode';
import type { VectorExecutionEngine } from './executionProtocol';

export interface VectorAnalysisSettings {
  readonly liveDiagnostics: boolean;
  readonly programRoot?: string;
}

export interface VectorExecutionSettings {
  readonly defaultEngine: VectorExecutionEngine;
  readonly pluginPaths: readonly string[];
  readonly programRoot?: string;
}

export function getAnalysisSettings(): VectorAnalysisSettings {
  const configuration = workspace.getConfiguration('vector');
  return {
    liveDiagnostics: configuration.get<boolean>('liveDiagnostics', true),
    programRoot: resolveOptionalPath(configuration.get<string>('programRoot', ''), undefined)
  };
}

export function getExecutionSettings(document: TextDocument): VectorExecutionSettings {
  const configuration = workspace.getConfiguration('vector', document.uri);
  const configuredPlugins = configuration.get<readonly string[]>('executionPluginPaths', []);
  return {
    defaultEngine: configuration.get<string>('defaultExecutionEngine', 'Interpreter').toLowerCase() === 'vm'
      ? 'vm'
      : 'interpreter',
    programRoot: resolveOptionalPath(configuration.get<string>('programRoot', ''), document),
    pluginPaths: configuredPlugins
      .map(path => resolveOptionalPath(path, document))
      .filter((path): path is string => path !== undefined)
  };
}

export function sourcePath(document: TextDocument): string | undefined {
  return document.uri.scheme === 'file' ? document.uri.fsPath : undefined;
}

export function defaultProgramRoot(document: TextDocument): string | undefined {
  const folder = workspace.getWorkspaceFolder(document.uri);
  if (folder?.uri.scheme === 'file') {
    return folder.uri.fsPath;
  }
  const path = sourcePath(document);
  return path === undefined ? undefined : dirname(path);
}

function resolveOptionalPath(value: string, document: TextDocument | undefined): string | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return undefined;
  }
  if (isAbsolute(trimmed)) {
    return resolve(trimmed);
  }

  if (document !== undefined) {
    return resolve(defaultProgramRoot(document) ?? process.cwd(), trimmed);
  }
  const firstWorkspace = workspace.workspaceFolders?.find(folder => folder.uri.scheme === 'file');
  return resolve(firstWorkspace?.uri.fsPath ?? process.cwd(), trimmed);
}

export function analysisEnvironment(settings: VectorAnalysisSettings): NodeJS.ProcessEnv {
  return {
    VECTOR_LIVE_DIAGNOSTICS: String(settings.liveDiagnostics),
    ...(settings.programRoot === undefined ? {} : { VECTOR_PROGRAM_ROOT: settings.programRoot })
  };
}

export function analysisSettingsKey(settings: VectorAnalysisSettings): string {
  return JSON.stringify(settings);
}
