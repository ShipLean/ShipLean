import { useState } from 'react';
import { MoreButton } from '../../../touch/MoreButton';
import { retroSharedTestIds as ids } from '../testIds';
import type { RetroNoteWrites } from './useRetroNoteWrites';

/**
 * The ⋯ menu; it shows only where the server says the point can be archived. With `onMoveTheme` (the touch layout) it is
 * K5's MoreButton (a 32px ⋯ opening an action sheet of Move to theme and Archive); without it, the desktop popover with Archive.
 */
export function NoteMore({
  pointId,
  writes,
  onMoveTheme,
}: {
  pointId: string;
  writes: RetroNoteWrites;
  onMoveTheme?: () => void;
}) {
  const [open, setOpen] = useState(false);
  if (onMoveTheme) {
    return (
      <span className="absolute top-0.5 right-0.5">
        <MoreButton
          actions={[
            { id: 'theme', label: 'Move to theme', onSelect: onMoveTheme },
            {
              id: 'archive',
              label: 'Archive',
              tone: 'danger',
              disabled: writes.archive.isPending,
              onSelect: () => writes.archive.mutate(pointId),
            },
          ]}
        />
      </span>
    );
  }
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
