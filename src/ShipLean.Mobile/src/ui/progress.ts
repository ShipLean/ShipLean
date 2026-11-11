import type { RollupCountDto } from '@shiplean/core/api/views';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import { isStoryLevel } from '@shiplean/core/api/workItemTypes';

// Ported from the web backlog (features/backlog/levels/countUnder.ts), on the row's roll-ups.

/** Open and closed items of the given types; a missing roll-up entry means none. */
export function countUnder(rollups: RollupCountDto[], ...types: WorkItemTypeId[]) {
  const entries = rollups.filter((r) => types.includes(r.type));
  const open = entries.reduce((sum, r) => sum + r.open, 0);
  const closed = entries.reduce((sum, r) => sum + r.closed, 0);
  return { open, closed, total: open + closed };
}

/**
 * Open and closed items at story level (stories, bugs, action items), with the open ones planned into the current
 * sprint and into another sprint. The server decides which sprint is current; the rest of open is in no sprint.
 */
export function countStoryLevelUnder(rollups: RollupCountDto[]) {
  const types = rollups.map((r) => r.type).filter(isStoryLevel);
  const entries = rollups.filter((r) => types.includes(r.type));
  return {
    ...countUnder(rollups, ...types),
    current: entries.reduce((sum, r) => sum + r.inCurrentSprint, 0),
    other: entries.reduce((sum, r) => sum + r.inOtherSprint, 0),
  };
}
