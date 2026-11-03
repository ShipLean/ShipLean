import { useState } from 'react';
import { retroSharedTestIds as ids } from '../testIds';
import type { RetroNoteWrites } from './useRetroNoteWrites';

/** The ⋯ menu; it shows only where the server says the point can be archived. */
export function NoteMore({ pointId, writes }: { pointId: string; writes: RetroNoteWrites }) {
  const [open, setOpen] = useState(false);
  return (
    <span className="absolute top-[3px] right-[3px]">
      <button
        type="button"
        data-testid={ids.retroNoteMore}
        aria-label="More"
        aria-expanded={open}
        className="inline-flex size-[18px] cursor-pointer items-center justify-center rounded-[var(--radius-xs)] border-0 bg-transparent p-0 text-ink-muted hover:bg-sunken hover:text-ink"
        onClick={() => setOpen(!open)}
      >
        ⋯
      </button>
      {open ? (
        <button
          type="button"
          data-testid={ids.retroNoteArchive}
          disabled={writes.archive.isPending}
          className="absolute right-0 z-10 mt-1 cursor-pointer rounded-[var(--radius-xs)] border border-edge bg-white px-2 py-0.5 text-[length:var(--text-xs)] whitespace-nowrap text-ink"
          onClick={() => {
            setOpen(false);
            writes.archive.mutate(pointId);
          }}
        >
          Archive
        </button>
      ) : null}
    </span>
  );
}
