import type { ExtensionContext } from 'vscode';

export async function activate(_context: ExtensionContext): Promise<void> {
  // Language-client startup is added in the dedicated LSP integration step.
}

export async function deactivate(): Promise<void> {
  // Lifecycle cleanup is added with the language-client integration.
}
