import { build } from 'esbuild';
import { mkdir } from 'node:fs/promises';

const production = process.argv.includes('--production');

await Promise.all([
  mkdir('dist', { recursive: true }),
  mkdir('dist-test/test/unit', { recursive: true }),
  mkdir('dist-test/test/process', { recursive: true })
]);

await build({
  entryPoints: ['src/extension.ts'],
  bundle: true,
  external: ['vscode'],
  format: 'cjs',
  platform: 'node',
  target: 'node20',
  outfile: 'dist/extension.js',
  minify: production,
  sourcemap: production ? false : true,
  sourcesContent: !production,
  logLevel: 'info'
});

await build({
  entryPoints: ['test/**/*.test.ts'],
  bundle: true,
  external: ['vscode'],
  format: 'cjs',
  platform: 'node',
  target: 'node20',
  outbase: '.',
  outdir: 'dist-test',
  sourcemap: true,
  logLevel: 'info'
});
