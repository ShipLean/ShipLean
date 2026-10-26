import { useState } from 'react';
import { featureTestIds as ids } from '../../testIds';

// The card's ⋯ menu: Edit title/note and Remove.
export function LinkCardMenu({ onEdit, onRemove }: { onEdit: () => void; onRemove: () => void }) {
  const [open, setOpen] = useState(false);
  const item = 'block w-full px-3 py-1.5 text-left text-[length:var(--text-sm)] hover:bg-surface-2';
  return (
    <div className="relative">
      <button
        type="button"
        aria-label="Link actions"
        aria-haspopup="menu"
        aria-expanded={open}
        className="h-6 rounded-control px-1.5 text-ink-muted hover:bg-surface-2 hover:text-ink"
        onClick={() => setOpen(!open)}
        onKeyDown={(event) => {
          if (event.key === 'Escape') setOpen(false);
        }}
      >
        ⋯
      </button>
      {open ? (
        <div
          role="menu"
          className="absolute right-0 z-10 mt-1 w-44 rounded-control border border-edge bg-panel py-1 shadow-panel"
        >
          <button
            type="button"
            role="menuitem"
            data-testid={ids.linkCardEdit}
            className={item}
            onClick={() => {
              setOpen(false);
              onEdit();
            }}
          >
            Edit title/note
          </button>
          <button
            type="button"
            role="menuitem"
            data-testid={ids.linkCardRemove}
            className={item}
            onClick={() => {
              setOpen(false);
              onRemove();
            }}
          >
            Remove
          </button>
        </div>
      ) : null}
    </div>
  );
}
