import type { ReferenceContext } from 'vscode';

// VS Code's compact Shift+F12 path re-queries two-location results without the
// declaration, then treats the sole current use as no navigable reference.
export function includeReferenceDeclarations(context: ReferenceContext): ReferenceContext {
  return context.includeDeclaration
    ? context
    : { ...context, includeDeclaration: true };
}
