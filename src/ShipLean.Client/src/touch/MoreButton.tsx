import { useState, type ReactNode } from 'react';
import { ActionSheet, type SheetAction, type SheetSlot } from './ActionSheet';
import { touchTestIds } from './testIds';

const plainClass = 'text-ink-muted hover:text-ink';
// The bordered ⋯ the toolbars use (K1).
const borderedClass = 'border border-edge bg-panel text-ink';

/**
 * The ⋯ button (K5, SHIP-805 rule 8): opens an `ActionSheet` of `actions`, titled `label`, with an optional `subtitle`
 * under the title and a `footer` footnote after the rows.
 */
export function MoreButton({
  actions,
  label = 'More',
  subtitle,
  footer,
  variant,
  children,
}: {
  actions: readonly SheetAction[];
  label?: string;
  subtitle?: string;
  footer?: ReactNode;
  variant?: 'bordered';
  // Shown in the sheet before the action rows.
  children?: SheetSlot;
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
        className={`inline-grid size-[var(--touch-target)] flex-none place-items-center rounded-control text-[length:var(--text-md)] leading-none ${variant === 'bordered' ? borderedClass : plainClass} active:bg-signal-soft`}
        onClick={() => setOpen(true)}
      >
        ⋯
      </button>
      <ActionSheet
        open={open}
        title={label}
        subtitle={subtitle}
        actions={actions}
        footer={footer}
        onClose={() => setOpen(false)}
      >
        {children}
      </ActionSheet>
    </>
  );
}
