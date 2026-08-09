import { Card } from '../../components/ui/Card';
import { ErrorBlock, LoadingBlock } from '../../components/ui/States';
import { formatCurrency, formatRelativeTime } from '../../lib/format';
import type { DailyBalance } from '../../api/types';
import type { SyncPhase } from './syncPhase';

interface BalanceCardProps {
  date: string;
  onDateChange: (date: string) => void;
  balance?: DailyBalance;
  isLoading: boolean;
  error: unknown;
  onRetry: () => void;
  phase: SyncPhase;
  /** Injected in tests so the relative label is deterministic. */
  now?: Date;
}

function Figure({
  label,
  value,
  tone,
  testId,
}: {
  label: string;
  value: number;
  tone?: 'credit' | 'debit';
  testId?: string;
}) {
  const color =
    tone === 'credit' ? 'text-success' : tone === 'debit' ? 'text-danger' : 'text-foreground';
  return (
    <div>
      <p className="text-xs font-medium tracking-wide text-muted uppercase">{label}</p>
      <p data-testid={testId} className={`mt-1 text-2xl font-semibold ${color}`}>
        {formatCurrency(value)}
      </p>
    </div>
  );
}

/**
 * The consolidation status line. This is the element the whole frontend exists
 * for: it turns `updatedAt` — a field that reads as metadata in JSON — into the
 * visible evidence of eventual consistency (ADR-006).
 */
function StatusLine({
  phase,
  updatedAt,
  now,
}: {
  phase: SyncPhase;
  updatedAt: string | null;
  now?: Date;
}) {
  if (phase === 'syncing') {
    return (
      <p className="flex items-center gap-2 text-xs text-primary">
        <span
          aria-hidden="true"
          className="size-3 animate-spin rounded-full border-2 border-primary/30 border-t-primary"
        />
        Sincronizando com a consolidação…
      </p>
    );
  }

  if (phase === 'stale') {
    return (
      <p className="text-xs text-danger">
        Consolidação atrasada — o lançamento foi registrado e o saldo ainda não convergiu.
      </p>
    );
  }

  if (updatedAt === null) {
    return <p className="text-xs text-muted">Dia sem movimentação.</p>;
  }

  return (
    <p className="text-xs text-muted">Consolidação atualizada {formatRelativeTime(updatedAt, now)}</p>
  );
}

export function BalanceCard({
  date,
  onDateChange,
  balance,
  isLoading,
  error,
  onRetry,
  phase,
  now,
}: BalanceCardProps) {
  return (
    <Card>
      <header className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-base font-semibold text-foreground">Saldo do dia</h2>
        <label className="flex items-center gap-2 text-xs text-muted">
          <span>Data</span>
          <input
            type="date"
            value={date}
            onChange={(event) => onDateChange(event.target.value)}
            className="h-9 rounded-field border border-border-strong bg-background px-3 text-sm text-foreground"
            aria-label="Data do saldo"
          />
        </label>
      </header>

      {error ? (
        <ErrorBlock error={error} onRetry={onRetry} />
      ) : isLoading || !balance ? (
        <LoadingBlock label="Carregando saldo…" />
      ) : (
        <>
          <div className="grid gap-6 sm:grid-cols-3">
            <Figure label="Saldo" value={balance.balance} testId="balance-value" />
            <Figure label="Créditos" value={balance.totalCredits} tone="credit" />
            <Figure label="Débitos" value={balance.totalDebits} tone="debit" />
          </div>
          <div className="mt-4">
            <StatusLine phase={phase} updatedAt={balance.updatedAt} now={now} />
          </div>
        </>
      )}
    </Card>
  );
}
