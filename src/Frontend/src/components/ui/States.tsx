import { ApiError } from '../../api/http';
import { Button } from './Button';

export function LoadingBlock({ label = 'Carregando…' }: { label?: string }) {
  return (
    <div role="status" className="flex items-center gap-3 py-8 text-sm text-muted">
      <span
        aria-hidden="true"
        className="size-4 animate-spin rounded-full border-2 border-border-strong border-t-primary"
      />
      {label}
    </div>
  );
}

export function EmptyBlock({ message }: { message: string }) {
  return <p className="py-8 text-center text-sm text-muted">{message}</p>;
}

interface ErrorBlockProps {
  error: unknown;
  onRetry?: () => void;
}

/**
 * A failed region, contained. It renders inside the card that failed, so the
 * rest of the screen keeps working — which is how the independence of the two
 * contexts becomes visible (RNF-001).
 *
 * The `correlationId` is shown because it is the only thing that turns "deu
 * erro" into something findable in the structured logs (ADR-011).
 */
export function ErrorBlock({ error, onRetry }: ErrorBlockProps) {
  const apiError = error instanceof ApiError ? error : undefined;
  const message =
    apiError?.message ?? (error instanceof Error ? error.message : 'Falha inesperada.');

  return (
    <div role="alert" className="rounded-field bg-danger-surface p-4 text-sm">
      <p className="font-medium text-danger">{message}</p>
      {apiError?.correlationId && (
        <p className="mt-1 text-xs text-muted">
          Correlação: <code className="font-mono">{apiError.correlationId}</code>
        </p>
      )}
      {onRetry && (
        <Button variant="secondary" size="sm" className="mt-3" onClick={onRetry}>
          Tentar novamente
        </Button>
      )}
    </div>
  );
}
