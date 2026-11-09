import type { BacklogRowDto } from '@shiplean/core/api/views';
import { isStoryLevel } from '@shiplean/core/api/workItemTypes';

/** The rows shown: everything under a collapsed row (the rows deeper than it, up to the next row as shallow) is hidden. */
export function visibleRows(
  rows: readonly BacklogRowDto[],
  collapsed: ReadonlySet<string>,
): BacklogRowDto[] {
  const shown: BacklogRowDto[] = [];
  let hiddenBelow: number | null = null;
  for (const row of rows) {
    if (hiddenBelow !== null) {
      if (row.depth > hiddenBelow) continue;
      hiddenBelow = null;
    }
    shown.push(row);
    if (collapsed.has(row.id)) hiddenBelow = row.depth;
  }
  return shown;
}

/** "N items · Σ P pts": the stories and bugs under a group (closed ones only when Completed is on) and the points under them, open and closed (the Features backlog and board sum the same way). */
export function rollupText(row: BacklogRowDto, includeCompleted: boolean): string {
  const count = row.rollups
    .filter((r) => isStoryLevel(r.type))
    .reduce((sum, r) => sum + r.open + (includeCompleted ? r.closed : 0), 0);
  const items = `${count} ${count === 1 ? 'item' : 'items'}`;
  return row.childPoints === null ? items : `${items} · Σ ${row.childPoints} pts`;
}
