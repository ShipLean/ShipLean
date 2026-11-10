// The one place rank arithmetic lives. Every area that moves a row in rank order (Backlog, Features) plans the move here.
import type { BulkPlacement } from '@shiplean/core/api/backlogWrites';

export interface RankShift {
  key: string;
  from: number;
  to: number;
}

export type RankTarget =
  | { to: 'top' }
  | { to: 'bottom' }
  | { to: 'up' | 'down'; by: number }
  | { to: 'above' | 'below'; otherId: string }
  | { to: 'rank'; rank: number };

export interface RankPlan {
  placement: BulkPlacement;
  // Puts the row back where it was, once the move has been made.
  undoPlacement: BulkPlacement;
  fromRank: number;
  toRank: number;
  // The other rows whose rank changes.
  shifted: RankShift[];
}

export interface RankRow {
  id: string;
  key: string;
  rank: number;
}

const first: BulkPlacement = { slot: 'first', beforeItemId: null };
const last: BulkPlacement = { slot: 'last', beforeItemId: null };
const before = (id: string): BulkPlacement => ({ slot: 'before', beforeItemId: id });

// Before the row that follows `index` in `order`, or last when nothing follows.
function beforeNext(order: RankRow[], index: number): BulkPlacement {
  const next = order[index + 1];
  return next === undefined ? last : before(next.id);
}

// Where the row ends up, as an index in the list without the row. null when the target names no row.
function targetIndex(
  rows: RankRow[],
  rest: RankRow[],
  from: number,
  target: RankTarget,
): number | null {
  switch (target.to) {
    case 'top':
      return 0;
    case 'bottom':
      return rest.length;
    case 'up':
      return Math.max(0, from - target.by);
    case 'down':
      return Math.min(rest.length, from + target.by);
    case 'above':
    case 'below': {
      const at = rest.findIndex((r) => r.id === target.otherId);
      if (at < 0) return null;
      return target.to === 'above' ? at : at + 1;
    }
    case 'rank': {
      // Rank n names a slot in the list as it is now, so look it up there, then clamp to the list without the row.
      const at = rows.findIndex((r) => r.rank >= target.rank);
      return at < 0 ? rest.length : Math.min(at, rest.length);
    }
  }
}

// rows: one level's items in rank order, rank never null. Returns null when nothing moves or the target is unknown.
export function rankPlan(rows: RankRow[], itemId: string, target: RankTarget): RankPlan | null {
  const from = rows.findIndex((r) => r.id === itemId);
  if (from < 0) return null;
  const item = rows[from];
  const rest = rows.filter((r) => r.id !== itemId);
  const to = targetIndex(rows, rest, from, target);
  if (to === null || to === from) return null;

  const order = [...rest.slice(0, to), item, ...rest.slice(to)];
  // A row takes the rank of the slot it lands in.
  const shifted: RankShift[] = [];
  order.forEach((row, i) => {
    const rank = rows[i].rank;
    if (row.id !== itemId && row.rank !== rank)
      shifted.push({ key: row.key, from: row.rank, to: rank });
  });

  let placement: BulkPlacement;
  if (target.to === 'top') placement = first;
  else if (target.to === 'bottom') placement = last;
  else if (target.to === 'above') placement = before(target.otherId);
  else placement = to === 0 ? first : beforeNext(order, to);

  return {
    placement,
    undoPlacement: beforeNext(rows, from),
    fromRank: item.rank,
    toRank: rows[to].rank,
    shifted,
  };
}
