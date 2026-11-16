import { useState } from 'react';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import { MoreButton } from '../../../touch/MoreButton';
import { retroSharedTestIds as ids } from '../testIds';
import { NotePreview } from './NotePreview';
import type { RetroNoteWrites } from './useRetroNoteWrites';

/**
 * The ⋯ menu; it shows only where the server says the point can be archived. With `onMoveTheme` (the touch layout) it is
 * K5's MoreButton (a 32px ⋯ opening a sheet titled Point: the point, Move to theme… and Archive point); without it, the desktop popover with Archive.
 */
export function NoteMore({
  point,
  writes,
  onMoveTheme,
}: {
  point: RetroPointDto;
  writes: RetroNoteWrites;
  onMoveTheme?: () => void;
}) {
  const pointId = point.id;
  const [open, setOpen] = useState(false);
  if (onMoveTheme) {
    return (
      <span className="absolute top-0.5 right-0.5">
        <MoreButton
          label="Point"
          footer="Only your own points can be archived. An agent's points are never edited or archived."
          actions={[
            {
              id: 'theme',
              label: 'Move to theme…',
              detail: `# ${point.theme?.name ?? 'Unthemed'}`,
              onSelect: onMoveTheme,
            },
            {
              id: 'archive',
              label: 'Archive point',
              tone: 'danger',
              disabled: writes.archive.isPending,
              onSelect: () => writes.archive.mutate(pointId),
            },
          ]}
        >
          <NotePreview point={point} meta="your point" />
        </MoreButton>
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
