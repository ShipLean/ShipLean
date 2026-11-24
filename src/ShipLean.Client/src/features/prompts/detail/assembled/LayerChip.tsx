import type { ReactNode } from 'react';

const base =
  'inline-flex h-[18px] shrink-0 items-center gap-1 rounded-full px-2 text-[length:var(--text-2xs)] font-semibold whitespace-nowrap';

export type LayerChipTone = 'fixed' | 'custom' | 'planned' | 'ok';

const tone: Record<LayerChipTone, string> = {
  fixed: 'bg-sunken text-ink-muted',
  planned: 'bg-sunken text-ink-muted',
  custom: 'bg-[color-mix(in_srgb,var(--color-label-purple)_18%,transparent)] text-ink',
  ok: 'bg-[color-mix(in_srgb,var(--color-good)_18%,transparent)] text-ink',
};

// A small pill: Fixed, Custom vN, Planned, Applies cleanly.
export function LayerChip(props: { tone: LayerChipTone; testId?: string; children: ReactNode }) {
  return (
    <span
      data-testid={props.testId}
      data-tone={props.tone}
      className={`${base} ${tone[props.tone]}`}
    >
      {props.tone === 'custom' ? (
        <span aria-hidden="true" className="size-1.5 rounded-full bg-label-purple" />
      ) : null}
      {props.children}
    </span>
  );
}
