import { bulkPlace, getBacklog, type BacklogFilter } from '@shiplean/core/api/backlog';
import type { BulkPlacement, BulkWriteResult } from '@shiplean/core/api/backlogWrites';
import { ApiRequestError } from '@shiplean/core/api/client';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { Api, Pending, PreviewLine } from '../../actions/types';
import type { BacklogMove } from '../actions';

// The whole level, closed rows included. A row's rank depends on includeCompleted, so the checks below
// compare places, never rank numbers, and then they hold whichever list the person was looking at.
const LEVEL: BacklogFilter = { mode: 'list', filter: '', includeCompleted: true };

// A stale row is not an error to the bulk route; it is one to the runner, which turns the 409 into a conflict.
function refuseStale(result: BulkWriteResult): void {
  const [stale] = result.stale;
  if (stale === undefined) return;
  throw new ApiRequestError(stale.error, 409, 'conflict', stale);
}

async function place(a: BacklogMove, placement: BulkPlacement, expectedRevision: number) {
  refuseStale(
    await bulkPlace(a.projectId, [{ id: a.itemId, expectedRevision, changes: null, placement }]),
  );
}

// The calls go through @shiplean/core, so the runner's api argument is unused here.
// One position-only placement: changes is null, so the server checks no revision.
export async function sendMove(a: BacklogMove, api: Api): Promise<void> {
  void api;
  await place(a, a.placement, a.revision);
}

// "SHIP-906 4 → 2", the rows that shift under it.
export function describeMove(a: BacklogMove): PreviewLine {
  return {
    id: `move:${a.itemId}`,
    icon: 'rank',
    text: `${a.key} ${a.fromRank} → ${a.toRank}`,
    detail:
      a.shifted.length === 0
        ? undefined
        : a.shifted.map((s) => `${s.key} ${s.from} → ${s.to}`).join('\n'),
    warning: a.warning ?? undefined,
    removable: true,
  };
}

// The moved item's row, if it still sits where the move put it: first, last, or just above the row it was placed before.
export function stillPlaced(rows: BacklogRowDto[], a: BacklogMove): BacklogRowDto | null {
  const ranked = rows
    .filter((r): r is BacklogRowDto & { rank: number } => r.rank !== null)
    .sort((x, y) => x.rank - y.rank);
  const i = ranked.findIndex((r) => r.id === a.itemId);
  if (i < 0) return null;
  const { slot, beforeItemId } = a.placement;
  const fits =
    slot === 'first'
      ? i === 0
      : slot === 'last'
        ? i === ranked.length - 1
        : ranked[i + 1]?.id === beforeItemId;
  return fits ? ranked[i] : null;
}

// Puts the row back above its old neighbour, but only while it is still where the move left it.
export async function undoMove(a: BacklogMove, api: Api): Promise<void> {
  void api;
  const view = await getBacklog(a.projectId, a.level, LEVEL);
  const row = stillPlaced(view.rows, a);
  if (row === null) throw new Error(`${a.key} has moved since, so the move can't be undone.`);
  await place(a, a.undoPlacement, row.revision);
}

// An uncertain move went through when its item still sits where the move put it. Each level is read once.
export async function reconcileMove(items: Pending[], api: Api): Promise<('sent' | 'notSent')[]> {
  void api;
  const levels = new Map<string, ReturnType<typeof getBacklog>>();
  return Promise.all(
    items.map(async ({ action }) => {
      const a = action as BacklogMove;
      const scope = `${a.projectId}|${a.level}`;
      const read = levels.get(scope) ?? getBacklog(a.projectId, a.level, LEVEL);
      levels.set(scope, read);
      return stillPlaced((await read).rows, a) === null ? 'notSent' : 'sent';
    }),
  );
}
