import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes } from 'react';
import { useId } from 'react';
import { cn } from '../../lib/cn';

const control =
  'h-11 w-full rounded-field border bg-background px-4 text-sm text-foreground ' +
  'placeholder:text-muted disabled:opacity-50';

const borderFor = (invalid: boolean): string =>
  invalid ? 'border-danger' : 'border-border-strong';

interface LabelledProps {
  label: string;
  /** Messages from the `errors` object of a 400 Problem Details response. */
  errors?: string[];
  hint?: string;
}

function FieldShell({
  label,
  errors,
  hint,
  controlId,
  describedBy,
  children,
}: LabelledProps & { controlId: string; describedBy?: string; children: ReactNode }) {
  const invalid = Boolean(errors?.length);
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={controlId} className="text-sm font-medium text-foreground">
        {label}
      </label>
      {children}
      {hint && !invalid && (
        <p id={`${describedBy}-hint`} className="text-xs text-muted">
          {hint}
        </p>
      )}
      {invalid && (
        <p id={`${describedBy}-error`} role="alert" className="text-xs text-danger">
          {errors?.join(' ')}
        </p>
      )}
    </div>
  );
}

type InputProps = LabelledProps & InputHTMLAttributes<HTMLInputElement>;

export function Input({ label, errors, hint, className, ...rest }: InputProps) {
  const id = useId();
  const invalid = Boolean(errors?.length);
  return (
    <FieldShell label={label} errors={errors} hint={hint} controlId={id} describedBy={id}>
      <input
        id={id}
        aria-invalid={invalid || undefined}
        aria-describedby={invalid ? `${id}-error` : hint ? `${id}-hint` : undefined}
        className={cn(control, borderFor(invalid), className)}
        {...rest}
      />
    </FieldShell>
  );
}

type SelectProps = LabelledProps &
  SelectHTMLAttributes<HTMLSelectElement> & { children: ReactNode };

export function Select({ label, errors, hint, className, children, ...rest }: SelectProps) {
  const id = useId();
  const invalid = Boolean(errors?.length);
  return (
    <FieldShell label={label} errors={errors} hint={hint} controlId={id} describedBy={id}>
      <select
        id={id}
        aria-invalid={invalid || undefined}
        aria-describedby={invalid ? `${id}-error` : undefined}
        className={cn(control, borderFor(invalid), className)}
        {...rest}
      >
        {children}
      </select>
    </FieldShell>
  );
}
