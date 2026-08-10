import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '../../components/ui/Button';
import { Input, Select } from '../../components/ui/Field';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../api/http';
import type { CreateTransactionInput, TransactionType } from '../../api/types';
import { todayAsCivilDate } from '../../lib/format';
import { maskAmount } from './amountMask';
import {
  parseAmount,
  validateTransactionDraft,
  type FieldErrors,
  type TransactionDraft,
} from './validateTransaction';

interface TransactionFormProps {
  open: boolean;
  onClose: () => void;
  onSubmit: (input: CreateTransactionInput) => void;
  isSubmitting: boolean;
  error: unknown;
}

const emptyDraft = (): TransactionDraft => ({
  type: 'CREDIT',
  amount: '',
  occurredOn: todayAsCivilDate(),
  description: '',
});

export function TransactionForm({
  open,
  onClose,
  onSubmit,
  isSubmitting,
  error,
}: TransactionFormProps) {
  const [draft, setDraft] = useState<TransactionDraft>(emptyDraft);
  const [localErrors, setLocalErrors] = useState<FieldErrors>({});

  useEffect(() => {
    if (open) {
      setDraft(emptyDraft());
      setLocalErrors({});
    }
  }, [open]);

  const apiError = error instanceof ApiError ? error : undefined;
  // The server is the authority: its field errors are shown alongside the local
  // ones, never instead of them.
  const errorsFor = (field: string): string[] | undefined => {
    const merged = [...(localErrors[field] ?? []), ...(apiError?.fieldErrors?.[field] ?? [])];
    return merged.length > 0 ? merged : undefined;
  };

  const unexpectedFailure = apiError && !apiError.fieldErrors;

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();

    const errors = validateTransactionDraft(draft);
    setLocalErrors(errors);
    if (Object.keys(errors).length > 0) return;

    onSubmit({
      type: draft.type,
      amount: parseAmount(draft.amount),
      occurredOn: draft.occurredOn || undefined,
      description: draft.description || undefined,
    });
  };

  return (
    <Modal open={open} title="Novo lançamento" onClose={onClose}>
      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
        <Select
          label="Tipo"
          value={draft.type}
          onChange={(event) =>
            setDraft((current) => ({
              ...current,
              type: event.target.value as TransactionType,
            }))
          }
          errors={errorsFor('type')}
        >
          <option value="CREDIT">Crédito</option>
          <option value="DEBIT">Débito</option>
        </Select>

        <Input
          label="Valor"
          inputMode="numeric"
          autoComplete="off"
          placeholder="0,00"
          value={draft.amount}
          onChange={(event) =>
            setDraft((current) => ({ ...current, amount: maskAmount(event.target.value) }))
          }
          errors={errorsFor('amount')}
          hint="Somente números — os dois últimos dígitos são os centavos."
        />

        <Input
          label="Data"
          type="date"
          value={draft.occurredOn}
          onChange={(event) =>
            setDraft((current) => ({ ...current, occurredOn: event.target.value }))
          }
          errors={errorsFor('occurredAt')}
        />

        <Input
          label="Descrição"
          maxLength={200}
          placeholder="Opcional"
          value={draft.description}
          onChange={(event) =>
            setDraft((current) => ({ ...current, description: event.target.value }))
          }
          errors={errorsFor('description')}
        />

        {unexpectedFailure && (
          <div role="alert" className="rounded-field bg-danger-surface p-3 text-sm">
            <p className="font-medium text-danger">{apiError.message}</p>
            {apiError.correlationId && (
              <p className="mt-1 text-xs text-muted">
                Correlação: <code className="font-mono">{apiError.correlationId}</code>
              </p>
            )}
          </div>
        )}

        <div className="mt-1 flex justify-end gap-3">
          <Button variant="secondary" onClick={onClose} disabled={isSubmitting}>
            Cancelar
          </Button>
          <Button type="submit" loading={isSubmitting}>
            {isSubmitting ? 'Salvando…' : 'Salvar'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
