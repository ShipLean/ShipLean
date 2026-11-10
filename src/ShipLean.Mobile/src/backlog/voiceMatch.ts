// Naming an item by voice: a key, or a phrase from its title. Pure, so the voice spec and its tests share it.
import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { Match } from '../voice/contracts';
import { matchKey } from '../voice/resolve';

// Words people say around a title that aren't part of it ("the sign-in story", "the first one").
const FILLER = new Set(['the', 'story', 'feature', 'bug', 'epic', 'one']);

const words = (text: string): string[] => text.toLowerCase().match(/[\p{L}\p{N}']+/gu) ?? [];

// The rows whose title is the phrase (case aside), else every row whose title holds every word of the phrase once the
// filler words are left out. Rows come back in the order given. No words left means no match.
export function matchTitle<R extends { title: string }>(phrase: string, rows: R[]): R[] {
  const said = phrase.trim().toLowerCase();
  const exact = rows.filter((r) => r.title.trim().toLowerCase() === said);
  if (exact.length > 0) return exact;
  const wanted = words(phrase).filter((w) => !FILLER.has(w));
  if (wanted.length === 0) return [];
  return rows.filter((r) => {
    const have = new Set(words(r.title));
    return wanted.every((w) => have.has(w));
  });
}

export type ItemMatch =
  | { kind: 'one'; row: BacklogRowDto }
  // Two or more rows fit the phrase, in rank order.
  | { kind: 'many'; rows: BacklogRowDto[] }
  // Nothing fits; `key` is what the key matcher made of the phrase, for resolve.ts's unmatched.
  | { kind: 'none'; key: Match<string> };

const byRank = (a: BacklogRowDto, b: BacklogRowDto) =>
  (a.rank ?? Number.MAX_SAFE_INTEGER) - (b.rank ?? Number.MAX_SAFE_INTEGER);

// A key (as written or said) wins; otherwise a title phrase.
export function matchItem(spoken: string, rows: BacklogRowDto[]): ItemMatch {
  const items = rows.filter((r) => r.kind === 'item');
  const key = matchKey(
    spoken,
    items.map((r) => r.key),
  );
  if (key.kind === 'match') {
    const row = items.find((r) => r.key === key.value);
    if (row !== undefined) return { kind: 'one', row };
  }
  const titled = matchTitle(spoken, items);
  if (titled.length === 1) return { kind: 'one', row: titled[0] };
  if (titled.length > 1) return { kind: 'many', rows: [...titled].sort(byRank) };
  return { kind: 'none', key };
}

// The keys in rank order once `itemId` has taken the slot whose rank is `toRank`.
export function plannedKeys(
  order: readonly { id: string; key: string; rank: number }[],
  itemId: string,
  toRank: number,
): string[] {
  const item = order.find((r) => r.id === itemId);
  if (item === undefined) return order.map((r) => r.key);
  const rest = order.filter((r) => r.id !== itemId).map((r) => r.key);
  const at = order.findIndex((r) => r.rank === toRank);
  rest.splice(at < 0 ? rest.length : at, 0, item.key);
  return rest;
}

// "SHIP-906 is blocked by SHIP-907" when a blocker would sit below the moved row (D3), else null. A blocker that isn't in
// the list never warns.
export function blockedLine(
  key: string,
  blockedBy: readonly string[],
  planned: readonly string[],
): string | null {
  const at = planned.indexOf(key);
  const below = blockedBy.filter((b) => planned.indexOf(b) > at);
  return below.length === 0 ? null : `${key} is blocked by ${below.join(', ')}`;
}
