import { useState } from 'react';
import { ActionSheet, type SheetAction } from './ActionSheet';
import { touchTestIds } from './testIds';

/** The ⋯ button (K5, SHIP-805 rule 8): opens an `ActionSheet` of `actions`, titled `label`. */
export function MoreButton({
  actions,
  label = 'More',
}: {
  actions: readonly SheetAction[];
  label?: string;
}) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        data-testid={touchTestIds.moreButton}
        aria-label={label}
        aria-haspopup="dialog"
        aria-expanded={open}
        className="inline-grid size-[var(--touch-target)] flex-none place-items-center rounded-control text-[length:var(--text-md)] leading-none text-ink-muted hover:text-ink active:bg-signal-soft"
        onClick={() => setOpen(true)}
      >
        ⋯
      </button>
      <ActionSheet open={open} title={label} actions={actions} onClose={() => setOpen(false)} />
    </>
  );
}
