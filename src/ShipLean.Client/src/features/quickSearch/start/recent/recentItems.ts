import { STATE_CATEGORIES, WORK_ITEM_TYPE_IDS } from '../../../../api/refs';
import type { QuickSearchItem } from '../../types';

export const MAX_RECENT = 8;

const keyOf = (userId: string) => `shiplean.quickSearch.recent.${userId}`;

const isStrings = (value: unknown): value is string[] =>
  Array.isArray(value) && value.every((entry) => typeof entry === 'string');

function isItem(value: unknown): value is QuickSearchItem {
  if (typeof value !== 'object' || value === null) return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v.key === 'string' &&
    typeof v.title === 'string' &&
    typeof v.state === 'string' &&
    typeof v.projectKey === 'string' &&
    (WORK_ITEM_TYPE_IDS as readonly string[]).includes(v.type as string) &&
    (STATE_CATEGORIES as readonly string[]).includes(v.stateCategory as string) &&
    isStrings(v.parentPath)
  );
}

// The items opened in this browser by this user, newest first. Storage that throws or holds garbage reads as empty.
export function readRecent(userId: string): QuickSearchItem[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(keyOf(userId)) ?? '[]');
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(isItem).slice(0, MAX_RECENT);
  } catch {
    return [];
  }
}

// With moveToFront the item goes first. Without it, the stored copy is replaced in place, and an item not stored stays out.
export function rememberRecent(
  userId: string,
  item: QuickSearchItem,
  { moveToFront }: { moveToFront: boolean },
): void {
  const current = readRecent(userId);
  const at = current.findIndex((entry) => entry.key === item.key);
  let next: QuickSearchItem[];
  if (moveToFront) {
    next = [item, ...current.filter((entry) => entry.key !== item.key)].slice(0, MAX_RECENT);
  } else if (at >= 0) {
    next = current.map((entry, index) => (index === at ? item : entry));
  } else {
    return;
  }
  try {
    localStorage.setItem(keyOf(userId), JSON.stringify(next));
  } catch {
    // Storage is blocked or full: recents are a convenience.
  }
}
