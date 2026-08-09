import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { EmptyBlock, ErrorBlock, LoadingBlock } from '../../components/ui/States';
import { Table, TableBody, TableCell, TableHead, TableRow } from '../../components/ui/Table';
import { formatCurrency, formatDayMonth } from '../../lib/format';
import type { Transaction } from '../../api/types';

interface TransactionTableProps {
  items: Transaction[];
  isLoading: boolean;
  error: unknown;
  hasMore: boolean;
  isFetchingMore: boolean;
  onLoadMore: () => void;
  onRetry: () => void;
}

const COLUMNS = [
  { label: 'Data' },
  { label: 'Descrição' },
  { label: 'Tipo' },
  { label: 'Valor', align: 'right' as const },
];

export function TransactionTable({
  items,
  isLoading,
  error,
  hasMore,
  isFetchingMore,
  onLoadMore,
  onRetry,
}: TransactionTableProps) {
  if (error) return <ErrorBlock error={error} onRetry={onRetry} />;
  if (isLoading) return <LoadingBlock label="Carregando lançamentos…" />;
  if (items.length === 0) {
    // A period with no entries is a result, not an absence (§4.4).
    return <EmptyBlock message="Nenhum lançamento no período." />;
  }

  return (
    <>
      <Table>
        <TableHead columns={COLUMNS} />
        <TableBody>
          {items.map((item) => (
            <TableRow key={item.id}>
              <TableCell muted>{formatDayMonth(item.occurredAt)}</TableCell>
              <TableCell muted={!item.description}>
                {item.description ?? 'Sem descrição'}
              </TableCell>
              <TableCell>
                <Badge tone={item.type === 'CREDIT' ? 'positive' : 'negative'}>
                  {item.type === 'CREDIT' ? 'Crédito' : 'Débito'}
                </Badge>
              </TableCell>
              <TableCell align="right">
                {/*
                  No sign is printed. `amount` is always positive in the contract
                  and the sign derives from `type` (RN-003) — the badge already
                  carries it, and a second encoding could contradict the first.
                */}
                <span className={item.type === 'CREDIT' ? 'text-success' : 'text-danger'}>
                  {formatCurrency(item.amount)}
                </span>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {/*
        Cursor pagination, not numbered pages: the contract deliberately has no
        `totalCount`, and page numbers would require inventing one (ADR-014).
      */}
      {hasMore && (
        <div className="mt-4 flex justify-center">
          <Button variant="secondary" onClick={onLoadMore} loading={isFetchingMore}>
            Carregar mais
          </Button>
        </div>
      )}
    </>
  );
}
