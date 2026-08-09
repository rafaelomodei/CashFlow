import type { ReactNode } from 'react';

export function Table({ children }: { children: ReactNode }) {
  // The wrapper scrolls instead of the page: a wide table must not push the
  // whole layout sideways on a narrow screen.
  return (
    <div className="-mx-2 overflow-x-auto px-2">
      <table className="w-full min-w-[34rem] border-collapse text-sm">{children}</table>
    </div>
  );
}

export function TableHead({ columns }: { columns: ReadonlyArray<{ label: string; align?: 'right' }> }) {
  return (
    <thead>
      <tr className="border-b border-border">
        {columns.map((column) => (
          <th
            key={column.label}
            scope="col"
            className={`px-3 py-2.5 text-xs font-medium text-muted ${
              column.align === 'right' ? 'text-right' : 'text-left'
            }`}
          >
            {column.label}
          </th>
        ))}
      </tr>
    </thead>
  );
}

export function TableBody({ children }: { children: ReactNode }) {
  return <tbody>{children}</tbody>;
}

export function TableRow({ children }: { children: ReactNode }) {
  return <tr className="border-b border-border/60 last:border-0">{children}</tr>;
}

export function TableCell({
  children,
  align,
  muted,
}: {
  children: ReactNode;
  align?: 'right';
  muted?: boolean;
}) {
  return (
    <td
      className={`px-3 py-3 ${align === 'right' ? 'text-right' : 'text-left'} ${
        muted ? 'text-muted' : 'text-foreground'
      }`}
    >
      {children}
    </td>
  );
}
