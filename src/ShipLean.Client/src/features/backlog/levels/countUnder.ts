import type { BacklogRowDto } from '../../../api/views';
import type { WorkItemTypeId } from '../../../api/refs';
import { isStoryLevel } from '../../../api/workItemTypes';

/** Open and closed items of the types under a row (a nested Feature's included); a missing roll-up entry means none. */
export function countUnder(row: BacklogRowDto, ...types: WorkItemTypeId[]) {
  const entries = row.rollups.filter((r) => types.includes(r.type));
  const open = entries.reduce((sum, r) => sum + r.open, 0);
  const closed = entries.reduce((sum, r) => sum + r.closed, 0);
  return { open, closed, total: open + closed };
}

/** Open and closed items at story level (stories, bugs, action items) under a row. */
export function countStoryLevelUnder(row: BacklogRowDto) {
  return countUnder(row, ...row.rollups.map((r) => r.type).filter(isStoryLevel));
}
