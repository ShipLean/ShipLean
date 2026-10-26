import { ConflictBanner } from '../../../components/ConflictBanner';
import { backlogTestIds as ids } from '../testIds';
import type { ReorderConflict as Conflict } from './useReorder';

// A stale reorder: who changed the item, with Use mine (send the move again against the new revision) or Keep theirs.
export function ReorderConflict({
  conflict,
  onUseMine,
  onKeepTheirs,
}: {
  conflict: Conflict;
  onUseMine: () => void;
  onKeepTheirs: () => void;
}) {
  return (
    <div
      data-testid={ids.conflict}
      data-key={conflict.itemKey}
      className="flex-none px-[var(--space-4)] py-[var(--space-2)]"
    >
      <ConflictBanner
        changedBy={`${conflict.changedBy} (${conflict.itemKey})`}
        onUseMine={onUseMine}
        onKeepTheirs={onKeepTheirs}
      />
    </div>
  );
}
