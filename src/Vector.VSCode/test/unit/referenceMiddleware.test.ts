import assert from 'node:assert/strict';
import test from 'node:test';
import { includeReferenceDeclarations } from '../../src/referenceMiddleware';

test('keeps VS Code reference requests declaration-inclusive', () => {
  assert.equal(includeReferenceDeclarations({ includeDeclaration: true }).includeDeclaration, true);
  assert.equal(includeReferenceDeclarations({ includeDeclaration: false }).includeDeclaration, true);
});
