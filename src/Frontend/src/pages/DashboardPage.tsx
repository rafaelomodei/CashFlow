import { useMemo, useState } from 'react';
import { BalanceCard } from '../features/daily-balance/BalanceCard';
import { useDailyBalance } from '../features/daily-balance/useDailyBalance';
import { TransactionForm } from '../features/transactions/TransactionForm';
import { TransactionTable } from '../features/transactions/TransactionTable';
import { useCreateTransaction } from '../features/transactions/useCreateTransaction';
import { useTransactions, type PeriodFilter } from '../features/transactions/useTransactions';
import { Button } from '../components/ui/Button';
import { Card, CardHeader } from '../components/ui/Card';
import { todayAsCivilDate } from '../lib/format';

export function DashboardPage() {
  const [date, setDate] = useState(todayAsCivilDate);
  const [period, setPeriod] = useState<PeriodFilter>({});
  const [formOpen, setFormOpen] = useState(false);

  /**
   * `createdAt` of the registration whose consolidation is being awaited, or
   * null. Only set when the entry lands on the day currently on screen — a
   * backdated entry moves another day's balance, and waiting for this one would
   * poll until the deadline and then wrongly blame the worker.
   */
  const [awaitingSince, setAwaitingSince] = useState<string | null>(null);

  const balance = useDailyBalance(date, awaitingSince);
  const transactions = useTransactions(period);

  const create = useCreateTransaction({
    onRegistered: (created, affectedDay) => {
      setFormOpen(false);
      setAwaitingSince(affectedDay === date ? created.createdAt : null);
    },
  });

  const items = useMemo(
    () => transactions.data?.pages.flatMap((page) => page.items) ?? [],
    [transactions.data],
  );

  const changeDate = (next: string) => {
    setDate(next);
    // The pending wait belongs to the day being left behind.
    setAwaitingSince(null);
  };

  return (
    <div className="mx-auto max-w-5xl px-4 py-8 sm:px-6 sm:py-12">
      <header className="mb-8 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-foreground">Cash Flow</h1>
          <p className="text-sm text-muted">Lançamentos e saldo diário consolidado</p>
        </div>
      </header>

      <div className="flex flex-col gap-6">
        <BalanceCard
          date={date}
          onDateChange={changeDate}
          balance={balance.data}
          isLoading={balance.isPending}
          error={balance.error}
          onRetry={() => void balance.refetch()}
          phase={balance.phase}
        />

        <Card>
          <CardHeader
            title="Lançamentos"
            action={
              <div className="flex flex-wrap items-end gap-3">
                <label className="flex items-center gap-2 text-xs text-muted">
                  <span>De</span>
                  <input
                    type="date"
                    aria-label="Data inicial"
                    value={period.startDate ?? ''}
                    onChange={(event) =>
                      setPeriod((current) => ({
                        ...current,
                        startDate: event.target.value || undefined,
                      }))
                    }
                    className="h-9 rounded-field border border-border-strong bg-background px-3 text-sm text-foreground"
                  />
                </label>
                <label className="flex items-center gap-2 text-xs text-muted">
                  <span>até</span>
                  <input
                    type="date"
                    aria-label="Data final"
                    value={period.endDate ?? ''}
                    onChange={(event) =>
                      setPeriod((current) => ({
                        ...current,
                        endDate: event.target.value || undefined,
                      }))
                    }
                    className="h-9 rounded-field border border-border-strong bg-background px-3 text-sm text-foreground"
                  />
                </label>
                <Button onClick={() => setFormOpen(true)}>+ Novo lançamento</Button>
              </div>
            }
          />

          <TransactionTable
            items={items}
            isLoading={transactions.isPending}
            error={transactions.error}
            hasMore={Boolean(transactions.hasNextPage)}
            isFetchingMore={transactions.isFetchingNextPage}
            onLoadMore={() => void transactions.fetchNextPage()}
            onRetry={() => void transactions.refetch()}
          />
        </Card>
      </div>

      <TransactionForm
        open={formOpen}
        onClose={() => setFormOpen(false)}
        onSubmit={(input) => create.mutate(input)}
        isSubmitting={create.isPending}
        error={create.error}
      />
    </div>
  );
}
