import type { BacklogRowDto } from '../../../api/views';
import type { WorkItemTypeId } from '../../../api/refs';

/** Open and closed items of the types under a row (a nested Feature's included); a missing roll-up entry means none. */
export function countUnder(row: BacklogRowDto, ...types: WorkItemTypeId[]) {
  const entries = row.rollups.filter((r) => types.includes(r.type));
  const open = entries.reduce((sum, r) => sum + r.open, 0);
  const closed = entries.reduce((sum, r) => sum + r.closed, 0);
  return { open, closed, total: open + closed };
}
