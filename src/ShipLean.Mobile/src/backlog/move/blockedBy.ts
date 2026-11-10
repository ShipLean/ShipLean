// The warning a move carries when it would put an item above one it is blocked by (design decision D3).
export interface PlanRow {
  id: string;
  key: string;
  rank: number;
}

// The keys in the order the list will have once `itemId` takes the slot at `toRank`, in rank order today.
export function plannedKeys(rows: readonly PlanRow[], itemId: string, toRank: number): string[] {
  const item = rows.find((r) => r.id === itemId);
  if (item === undefined) return rows.map((r) => r.key);
  const rest = rows.filter((r) => r.id !== itemId);
  const at = rows.findIndex((r) => r.rank === toRank);
  const to = at < 0 ? rest.length : Math.min(at, rest.length);
  return [...rest.slice(0, to).map((r) => r.key), item.key, ...rest.slice(to).map((r) => r.key)];
}

// "SHIP-906 is blocked by SHIP-907" when a key in blockedBy ranks below the moved row in `planned`, else null. A blocker
// that is not in the list (another level, or closed) never warns.
export function blockedWarning(
  key: string,
  blockedBy: readonly string[],
  planned: readonly string[],
): string | null {
  const at = planned.indexOf(key);
  const below = blockedBy.filter((blocker) => {
    const place = planned.indexOf(blocker);
    return place >= 0 && place > at;
  });
  return below.length === 0 ? null : `${key} is blocked by ${below.join(', ')}`;
}
