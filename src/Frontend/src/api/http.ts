import type { ProblemDetails } from './types';

/**
 * Every failure the UI can face, as one type: validation, server error, and the
 * network itself. Without this, a component would have to tell a `TypeError`
 * from a Problem Details body before deciding what to render.
 */
export class ApiError extends Error {
  /** HTTP status, or `0` when the request never reached a server. */
  readonly status: number;
  readonly correlationId?: string;
  /** Present only on a 400 of validation — §4.2 of the contract. */
  readonly fieldErrors?: Record<string, string[]>;

  constructor(message: string, status: number, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.correlationId = problem?.correlationId;
    this.fieldErrors = problem?.errors;
  }

  get isNetworkFailure(): boolean {
    return this.status === 0;
  }
}

const newCorrelationId = (): string =>
  globalThis.crypto?.randomUUID?.() ??
  // Older browsers without `crypto.randomUUID` still get a traceable id; the
  // value only has to be unique enough to join log lines.
  `${Date.now().toString(16)}-${Math.random().toString(16).slice(2, 10)}`;

async function readProblemDetails(response: Response): Promise<ProblemDetails | undefined> {
  try {
    const body = (await response.json()) as ProblemDetails;
    return typeof body === 'object' && body !== null ? body : undefined;
  } catch {
    // A gateway error is HTML, not Problem Details. Failing to parse it must not
    // replace the real status with a parse error.
    return undefined;
  }
}

export async function request<T>(url: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('X-Correlation-Id', newCorrelationId());
  headers.set('Accept', 'application/json');
  if (init.body !== undefined) {
    headers.set('Content-Type', 'application/json; charset=utf-8');
  }

  let response: Response;
  try {
    response = await fetch(url, { ...init, headers });
  } catch {
    throw new ApiError(
      'Não foi possível falar com o servidor. Verifique se o serviço está no ar.',
      0,
    );
  }

  if (!response.ok) {
    const problem = await readProblemDetails(response);
    throw new ApiError(
      problem?.detail ?? `Falha na requisição (HTTP ${response.status}).`,
      response.status,
      problem,
    );
  }

  if (response.status === 204 || response.headers.get('Content-Length') === '0') {
    return undefined as T;
  }

  return (await response.json()) as T;
}
