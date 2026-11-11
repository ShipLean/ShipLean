import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import { isStoryLevel } from '@shiplean/core/api/workItemTypes';

/** Open and closed items of the types under a row (a nested Feature's included); a missing roll-up entry means none. */
export function countUnder(row: BacklogRowDto, ...types: WorkItemTypeId[]) {
  const entries = row.rollups.filter((r) => types.includes(r.type));
  const open = entries.reduce((sum, r) => sum + r.open, 0);
  const closed = entries.reduce((sum, r) => sum + r.closed, 0);
  return { open, closed, total: open + closed };
}

/**
 * Open and closed items at story level (stories, bugs, action items) under a row, with the open ones planned into the current
 * sprint and into another sprint. The server decides which sprint is current; the rest of open is in no sprint.
 */
export function countStoryLevelUnder(row: BacklogRowDto) {
  const types = row.rollups.map((r) => r.type).filter(isStoryLevel);
  const entries = row.rollups.filter((r) => types.includes(r.type));
  return {
    ...countUnder(row, ...types),
    current: entries.reduce((sum, r) => sum + r.inCurrentSprint, 0),
    other: entries.reduce((sum, r) => sum + r.inOtherSprint, 0),
  };
}
