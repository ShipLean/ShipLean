import { bulkPlace, getBacklog, type BacklogFilter } from '@shiplean/core/api/backlog';
import type { BulkPlacement, BulkWriteResult } from '@shiplean/core/api/backlogWrites';
import { ApiRequestError } from '@shiplean/core/api/client';
import type { Api, Pending, PreviewLine } from '../../actions/types';
import type { BacklogMove } from '../actions';

// The list the Backlog screen opens on: open items only, no criteria filter. Ranks are read from it.
const LEVEL: BacklogFilter = { mode: 'list', filter: '', includeCompleted: false };

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

// Puts the row back above its old neighbour, but only while it is still where the move left it.
export async function undoMove(a: BacklogMove, api: Api): Promise<void> {
  void api;
  const view = await getBacklog(a.projectId, a.level, LEVEL);
  const row = view.rows.find((r) => r.id === a.itemId);
  if (row === undefined || row.rank !== a.toRank)
    throw new Error(`${a.key} has moved since, so the move can't be undone.`);
  await place(a, a.undoPlacement, row.revision);
}

// An uncertain move went through when its item now sits at the rank it was moved to. Each level is read once.
export async function reconcileMove(items: Pending[], api: Api): Promise<('sent' | 'notSent')[]> {
  void api;
  const levels = new Map<string, ReturnType<typeof getBacklog>>();
  return Promise.all(
    items.map(async ({ action }) => {
      const a = action as BacklogMove;
      const scope = `${a.projectId}|${a.level}`;
      const read = levels.get(scope) ?? getBacklog(a.projectId, a.level, LEVEL);
      levels.set(scope, read);
      const row = (await read).rows.find((r) => r.id === a.itemId);
      return row?.rank === a.toRank ? 'sent' : 'notSent';
    }),
  );
}
