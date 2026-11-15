import { useState } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { treeTarget } from '../treeTarget';

/**
 * What a handle dragged over the touch tree does. `over(targetId)` lights the group header under the finger (`overId`;
 * null off a group). `drop(rowId, targetId)` finds both rows and asks `treeTarget` what the drop means; it calls
 * `onDrop(active, over)` (TreeMode's own drop, the one write the tree has) only when something would change, and sends
 * nothing for a drop on itself, on the group the row is already under or off any row.
 */
export function useTouchReparent(
  rows: readonly BacklogRowDto[],
  onDrop: (active: BacklogRowDto, over: BacklogRowDto) => void,
) {
  const [overId, setOverId] = useState<string | null>(null);
  const find = (id: string | null) => rows.find((row) => row.id === id);
  return {
    overId,
    over(targetId: string | null) {
      const target = find(targetId);
      setOverId(target !== undefined && target.kind !== 'item' ? target.id : null);
    },
    drop(rowId: string, targetId: string | null) {
      setOverId(null);
      const active = find(rowId);
      const over = find(targetId);
      if (active === undefined || over === undefined) return;
      if (treeTarget(rows, active, over) === null) return;
      onDrop(active, over);
    },
  };
}
