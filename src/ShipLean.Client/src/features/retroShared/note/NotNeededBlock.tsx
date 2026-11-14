import type { RetroNotNeededDto } from '@shiplean/core/api/retros';
import { retroSharedTestIds as ids } from '../testIds';
import type { RetroNoteWrites } from './useRetroNoteWrites';

export function NotNeededBlock({
  pointId,
  notNeeded,
  writes,
}: {
  pointId: string;
  notNeeded: RetroNotNeededDto;
  writes: RetroNoteWrites;
}) {
  return (
    <div
      data-testid={ids.retroNoteNone}
      className="grid gap-0.5 border-l-2 border-ink-muted px-1.5 py-1 text-[length:var(--text-xs)] text-ink-muted touch:text-[length:var(--text-sm)]"
    >
      <b className="font-semibold text-ink">Not needed</b>
      <span>{notNeeded.reason}</span>
      <span>
        {notNeeded.by.user.name.split(' ')[0]}
        {notNeeded.by.agentName ? ` · ${notNeeded.by.agentName}` : ''} · {notNeeded.decidedAtLabel}
        {' · '}
        <button
          type="button"
          data-testid={ids.retroNoteUndo}
          disabled={writes.undoNotNeeded.isPending}
          className="cursor-pointer border-0 bg-transparent p-0 text-ink-muted hover:underline touch:min-h-[var(--touch-target)] touch:min-w-[var(--touch-target)] touch:underline"
          onClick={() => writes.undoNotNeeded.mutate(pointId)}
        >
          Undo
        </button>
      </span>
    </div>
  );
}
