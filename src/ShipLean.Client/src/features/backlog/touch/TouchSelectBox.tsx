import type { BacklogRowDto } from '@shiplean/core/api/views';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';

// The select box in a row's handle slot while Select mode is on: the whole slot is its hit area.
export function TouchSelectBox({ row }: { row: BacklogRowDto }) {
  const selection = useSelection();
  return (
    <label className="flex size-full min-h-[var(--touch-target)] min-w-[var(--touch-target)] cursor-pointer items-center justify-center">
      <input
        type="checkbox"
        data-testid={ids.touchRowSelect}
        data-row-id={row.id}
        checked={selection.has(row.id)}
        onChange={() => selection.toggle(row.id)}
        aria-label={`Select ${row.key}`}
        className="size-4 accent-[var(--color-accent)]"
      />
    </label>
  );
}
