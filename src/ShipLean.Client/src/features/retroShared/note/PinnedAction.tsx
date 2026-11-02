import type { RetroActionRefDto } from '../../../api/retros';
import { ActionChip } from '../ActionChip';
import { retroSharedTestIds as ids } from '../testIds';

// TODO: the finished white pin styling replaces this plain chip row.
export function PinnedAction({ action }: { action: RetroActionRefDto }) {
  const others = action.pointCount - 1;
  return (
    <div data-testid={ids.retroNotePin} className="flex flex-wrap items-center gap-1.5">
      <ActionChip action={action} />
      {others > 0 ? (
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          +{others} more point{others === 1 ? '' : 's'}
        </span>
      ) : null}
    </div>
  );
}
