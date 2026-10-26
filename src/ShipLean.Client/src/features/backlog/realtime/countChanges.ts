import type { BacklogRowDto } from '../../../api/views';

type Row = Pick<BacklogRowDto, 'id' | 'revision'>;

// The ids in a longest run that keeps its relative order, given each id's place in the earlier read (patience sorting).
function keptOrder(ids: readonly string[], placeOf: ReadonlyMap<string, number>): Set<string> {
  const places = ids.map((id) => placeOf.get(id)!);
  const tails: number[] = [];
  const previous: number[] = [];
  places.forEach((place, i) => {
    let low = 0;
    let high = tails.length;
    while (low < high) {
      const mid = (low + high) >> 1;
      if (places[tails[mid]] < place) low = mid + 1;
      else high = mid;
    }
    previous[i] = low > 0 ? tails[low - 1] : -1;
    tails[low] = i;
  });
  const kept = new Set<string>();
  for (let at = tails.at(-1) ?? -1; at >= 0; at = previous[at]) kept.add(ids[at]);
  return kept;
}

/**
 * How many items differ between two reads of the backlog: added, removed, at another revision, or moved. A reorder
 * changes no revision, so it shows as order: of the items in both reads, those outside the longest stretch that kept
 * its relative order are the ones that moved (one drag counts one). An item is counted once.
 */
export function countChanges(before: readonly Row[], after: readonly Row[]): number {
  const was = new Map(before.map((row) => [row.id, row.revision]));
  const now = new Set(after.map((row) => row.id));
  const removed = before.filter((row) => !now.has(row.id)).length;

  const placeOf = new Map(before.filter((row) => now.has(row.id)).map((row, i) => [row.id, i]));
  const common = after.filter((row) => placeOf.has(row.id)).map((row) => row.id);
  const kept = keptOrder(common, placeOf);
  const changed = after.filter(
    (row) => was.get(row.id) !== row.revision || (placeOf.has(row.id) && !kept.has(row.id)),
  ).length;
  return changed + removed;
}

export function changesText(count: number): string {
  if (count === 0) return 'Back online. Nothing changed while you were away.';
  return `Back online. ${count === 1 ? '1 item' : `${count} items`} changed while you were away.`;
}
