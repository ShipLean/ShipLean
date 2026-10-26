import { boardTestIds as ids } from '../testIds';

// Expands a Done cell to every card it holds; the cell's cursor goes back to the server as `older`.
export function ShowOlder({
  count,
  cursor,
  onExpand,
}: {
  count: number;
  cursor: string;
  onExpand: (cursor: string) => void;
}) {
  return (
    <button
      type="button"
      data-testid={ids.showOlder}
      data-cursor={cursor}
      className="btn-ghost h-6 justify-center text-[length:var(--text-xs)] text-ink-muted"
      onClick={() => onExpand(cursor)}
    >
      Show older ({count})
    </button>
  );
}
