// What the Inbox screen and the tab badge show, as pure functions (D11, P13): which items are still waiting, and how many
// asks. An answer in the runner moves its asks out of the counts at once, and its items off the list once it is past its
// undo window.
import type { InboxDto, InboxItemDto } from '@shiplean/core/api/inbox';
import type { PendingState } from '../actions/types';
import type { PendingAnswer } from './actions';

// A failed answer changed nothing, so its asks still wait. An answer in its undo window can still be taken back, so its
// row stays on the list (with its Undo) while the counts already drop.
const COUNTED_OUT: PendingState[] = ['failed'];
const LISTED_OUT: PendingState[] = ['failed', 'undo'];

// The labels on this item that answers in none of the given states cover.
function answeredLabels(
  item: InboxItemDto,
  pending: PendingAnswer[],
  except: PendingState[],
): Set<string> {
  const labels = new Set<string>();
  for (const p of pending) {
    if (p.action.itemId !== item.id || except.includes(p.state)) continue;
    for (const label of p.labels) labels.add(label);
  }
  return labels;
}

// The item's asks no pending answer covers (any state but failed).
function remainingAsks(item: InboxItemDto, pending: PendingAnswer[]): number {
  const answered = answeredLabels(item, pending, COUNTED_OUT);
  return item.asks.filter((a) => !answered.has(a.label)).length;
}

// The items still on the list, in the server's order: an item leaves once every one of its asks has a pending answer
// that is neither in its undo window nor failed.
export function visibleItems(items: InboxItemDto[], pending: PendingAnswer[]): InboxItemDto[] {
  return items.filter((item) => {
    const answered = answeredLabels(item, pending, LISTED_OUT);
    return item.asks.length === 0 || item.asks.some((a) => !answered.has(a.label));
  });
}

export interface InboxCounts {
  asks: number;
  items: number;
}

// The asks and the items still waiting: OpenAsks less the asks answered in the runner (in any state but failed), and
// OpenItems less the items with nothing left to answer.
export function inboxCounts(inbox: InboxDto, pending: PendingAnswer[]): InboxCounts {
  let asks = inbox.openAsks;
  let items = inbox.openItems;
  for (const item of inbox.items) {
    const left = remainingAsks(item, pending);
    asks -= item.asks.length - left;
    if (left === 0) items -= 1;
  }
  return { asks: Math.max(0, asks), items: Math.max(0, items) };
}

// The tab badge: the asks waiting, or null (no badge) before the Inbox has loaded and when nothing waits.
export function badgeCount(inbox: InboxDto | undefined, pending: PendingAnswer[]): number | null {
  if (inbox === undefined) return null;
  const { asks } = inboxCounts(inbox, pending);
  return asks > 0 ? asks : null;
}

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

// The bar's second line: "15 asks on 6 items", "nothing waiting" once someone has had an ask, and nothing for a person
// who never has.
export function waitingSubtitle(counts: InboxCounts, everWaited: boolean): string | undefined {
  if (counts.asks > 0)
    return `${plural(counts.asks, 'ask', 'asks')} on ${plural(counts.items, 'item', 'items')}`;
  return everWaited ? 'nothing waiting' : undefined;
}

export const ANSWERED_SUBTITLE = 'answered by you this week';

export type EmptyKind = 'clear' | 'new';

// Why the Waiting list is empty, or null when it isn't: all clear (something has waited before) or a new person.
export function emptyKind(inbox: InboxDto, visible: InboxItemDto[]): EmptyKind | null {
  if (visible.length > 0) return null;
  return inbox.everWaited ? 'clear' : 'new';
}
