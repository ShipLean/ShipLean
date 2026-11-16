import { useState } from 'react';
import type { StateCategory } from '@shiplean/core/api/refs';
import { settingsTestIds } from '../../testIds';
import { AddStateForm } from '../AddStateForm';

/**
 * Add state at 900px and below, above the rows (SHIP-1809 P4): one "+ Add state" button that opens the name and category
 * form in its place. Adding or Cancel closes it again.
 */
export function AddStateTouch({
  taken,
  onAdd,
}: {
  taken(name: string): boolean;
  onAdd(name: string, category: StateCategory): void;
}) {
  const [open, setOpen] = useState(false);
  if (!open)
    return (
      <button
        type="button"
        data-testid={settingsTestIds.stateAddOpen}
        className="btn-ghost w-full text-ink"
        onClick={() => setOpen(true)}
      >
        + Add state
      </button>
    );
  return (
    <AddStateForm
      autoFocus
      taken={taken}
      onAdd={(name, category) => {
        onAdd(name, category);
        setOpen(false);
      }}
      onCancel={() => setOpen(false)}
    />
  );
}
