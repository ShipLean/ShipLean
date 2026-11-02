import { useState } from 'react';
import { retroSharedTestIds as ids } from '../testIds';
import type { RetroNoteWrites } from './useRetroNoteWrites';

// TODO: the finished More menu styling replaces this plain menu; it shows only where the server says the point can be archived.
export function NoteMore({ pointId, writes }: { pointId: string; writes: RetroNoteWrites }) {
  const [open, setOpen] = useState(false);
  return (
    <span className="absolute top-1 right-1">
      <button
        type="button"
        data-testid={ids.retroNoteMore}
        aria-label="More"
        aria-expanded={open}
        className="cursor-pointer border-0 bg-transparent p-0 text-ink-muted hover:text-ink"
        onClick={() => setOpen(!open)}
      >
        ⋯
      </button>
      {open ? (
        <button
          type="button"
          data-testid={ids.retroNoteArchive}
          disabled={writes.archive.isPending}
          className="absolute right-0 z-10 mt-1 cursor-pointer rounded-sm border border-edge bg-panel px-2 py-0.5 text-[length:var(--text-xs)] whitespace-nowrap text-ink"
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
