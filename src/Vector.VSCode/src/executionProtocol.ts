export type VectorExecutionEngine = 'interpreter' | 'vm';
export type VectorExecutionOperation = 'run' | 'disassemble';

export interface VectorExecutionRequest {
  readonly source: string;
  readonly sourcePath?: string;
  readonly programRoot?: string;
  readonly engine: VectorExecutionEngine;
  readonly operation: VectorExecutionOperation;
  readonly pluginPaths: readonly string[];
}

export interface VectorProtocolPosition {
  readonly offset: number;
  readonly line: number;
  readonly character: number;
}

export interface VectorProtocolDiagnostic {
  readonly code: string;
  readonly severity: string;
  readonly message: string;
  readonly sourceName?: string | null;
  readonly range: {
    readonly start: VectorProtocolPosition;
    readonly end: VectorProtocolPosition;
  };
}

export interface VectorExecutionResponse {
  readonly success: boolean;
  readonly engine: VectorExecutionEngine;
  readonly operation: VectorExecutionOperation;
  readonly output: readonly string[];
  readonly result?: string | null;
  readonly diagnostics: readonly VectorProtocolDiagnostic[];
  readonly disassembly?: string | null;
  readonly hostFailure?: {
    readonly code: string;
    readonly message: string;
  } | null;
}
