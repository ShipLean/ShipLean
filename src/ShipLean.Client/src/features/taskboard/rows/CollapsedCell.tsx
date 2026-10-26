import { taskboardTestIds as ids } from '../testIds';

/** A cell of a collapsed row: only the number of tasks it holds. */
export function CollapsedCell({ count }: { count: number }) {
  return (
    <div className="border-l border-edge bg-sunken px-2 py-1 text-[length:var(--text-xs)] text-ink-muted">
      <span data-testid={ids.cellCount}>{count > 0 ? count : ''}</span>
    </div>
  );
}
