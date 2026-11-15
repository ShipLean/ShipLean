import { useState } from 'react';
import { Sheet } from '../../../touch/Sheet';
import { backlogTestIds as ids } from '../testIds';

/** The bar's Tags sheet: a 16px text field and Add; the tag joins each selected row's own tags. */
export function BulkTagSheet({
  onAdd,
  onClose,
}: {
  onAdd: (tag: string) => void;
  onClose: () => void;
}) {
  const [text, setText] = useState('');
  const tag = text.trim();
  const add = () => {
    if (tag === '') return;
    onAdd(tag);
    onClose();
  };
  return (
    <Sheet open title="Add tag" onClose={onClose}>
      <div className="flex gap-2">
        <input
          data-testid={ids.bulkSheetTagInput}
          aria-label="Tag name"
          placeholder="Tag name"
          value={text}
          className="h-11 min-w-0 flex-1 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-base)]"
          onChange={(event) => setText(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              add();
            }
          }}
        />
        <button
          type="button"
          data-testid={ids.bulkSheetTagAdd}
          disabled={tag === ''}
          className="min-h-[var(--touch-target)] rounded-control border border-signal bg-signal px-4 text-[length:var(--text-base)] font-medium text-signal-ink disabled:border-edge disabled:bg-transparent disabled:text-ink-muted"
          onClick={add}
        >
          Add
        </button>
      </div>
    </Sheet>
  );
}
