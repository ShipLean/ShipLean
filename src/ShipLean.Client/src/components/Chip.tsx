import type { ReactNode } from 'react';
import { componentTestIds } from './testIds';

export type ChipTone = 'danger' | 'warn' | 'ok' | 'info' | 'accent' | 'muted' | 'purple';

function background(tone: ChipTone): string {
  switch (tone) {
    case 'muted':
      return 'var(--color-surface-2)';
    case 'accent':
      return 'var(--color-accent-soft)';
    default:
      return `color-mix(in srgb, var(--color-${tone}) 18%, transparent)`;
  }
}

export function Chip({
  tone = 'muted',
  children,
  onRemove,
}: {
  tone?: ChipTone;
  children: ReactNode;
  onRemove?: () => void;
}) {
  return (
    <span
      data-testid={componentTestIds.chip}
      data-tone={tone}
      className="inline-flex h-5 items-center gap-1 rounded-full px-[7px] text-[length:var(--text-xs)] font-medium whitespace-nowrap text-ink"
      style={{ background: background(tone) }}
    >
      {children}
      {onRemove ? (
        <button
          type="button"
          data-testid={componentTestIds.chipRemove}
          aria-label="Remove"
          // At touch the button is 32px square (SHIP-805 rule 13); the negative margins give back the extra size, so the chip
          // keeps its 20px height and its width.
          className="inline-flex items-center justify-center text-ink-muted hover:text-ink touch:-mx-[12.5px] touch:-my-1.5 touch:min-h-[var(--touch-target)] touch:min-w-[var(--touch-target)]"
          onClick={onRemove}
        >
          ×
        </button>
      ) : null}
    </span>
  );
}
