import type { ReactNode } from 'react';
import { cn } from '../../lib/cn';

// Vocabulário visual, não de domínio: um primitivo de UI não pode saber o
// que é crédito ou débito. Quem traduz é `features/`.
type Tone = 'positive' | 'negative' | 'neutral';

const tones: Record<Tone, string> = {
  positive: 'bg-success-surface text-success',
  negative: 'bg-danger-surface text-danger',
  neutral: 'bg-surface text-muted',
};

interface BadgeProps {
  tone: Tone;
  children: ReactNode;
}

export function Badge({ tone, children }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-pill px-2.5 py-1 text-xs font-medium',
        tones[tone],
      )}
    >
      {children}
    </span>
  );
}
