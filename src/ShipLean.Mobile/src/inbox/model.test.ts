import { describe, expect, it } from 'vitest';
import type { PendingState } from '../actions/types';
import { aPending } from '../test-support/actions';
import { aDecisionAsk, anInbox, anInboxItem, aQuestionAsk } from '../test-support/inbox';
import { designInbox } from '../test-support/inboxDesign';
import { labelsOf, type InboxAction, type PendingAnswer } from './actions';
import { badgeCount, emptyKind, inboxCounts, visibleItems, waitingSubtitle } from './model';

function answer(
  item: { id: string; key: string },
  action: Omit<Extract<InboxAction, { kind: 'approve' }>, 'area' | 'projectId' | 'itemId' | 'key'>,
  state: PendingState,
): PendingAnswer {
  const full: InboxAction = {
    area: 'inbox',
    projectId: 'p1',
    itemId: item.id,
    key: item.key,
    ...action,
  };
  return { ...aPending({ action: full, state }), action: full, labels: labelsOf(full) };
}

describe('inbox model', () => {
  const item = anInboxItem({
    asks: [aDecisionAsk({ number: 1 }), aDecisionAsk({ number: 2 }), aQuestionAsk({ number: 1 })],
  });
  const other = anInboxItem();

  it('hides an item once every ask has a pending answer past its undo window', () => {
    const approve = (numbers: number[], state: PendingState) =>
      answer(item, { kind: 'approve', numbers }, state);
    const reply: PendingAnswer = (() => {
      const full: InboxAction = {
        area: 'inbox',
        projectId: 'p1',
        itemId: item.id,
        key: item.key,
        kind: 'answer',
        number: 1,
        body: '50 MB',
      };
      return { ...aPending({ action: full, state: 'sent' }), action: full, labels: ['Q1'] };
    })();
    const items = [item, other];

    // Not every ask is covered yet.
    expect(visibleItems(items, [approve([1, 2], 'sent')])).toEqual(items);
    // Every ask is covered, but one answer is still inside its undo window: the row stays for its Undo.
    expect(visibleItems(items, [approve([1, 2], 'undo'), reply])).toEqual(items);
    // Past the window (queued, sending, uncertain or sent) the item leaves; other items stay.
    for (const state of ['queued', 'sending', 'uncertain', 'sent'] as const)
      expect(visibleItems(items, [approve([1, 2], state), reply])).toEqual([other]);
  });

  it('keeps an item whose pending answer failed', () => {
    const failed = answer(other, { kind: 'approve', numbers: [1] }, 'failed');
    expect(visibleItems([item, other], [failed])).toEqual([item, other]);
    // A failed answer is not counted as answered either.
    const inbox = anInbox({ items: [item, other] });
    expect(inboxCounts(inbox, [failed])).toEqual({ asks: 4, items: 2 });
  });

  it('counts asks, not items', () => {
    const inbox = anInbox({ items: [item, other] });
    expect(inboxCounts(inbox, [])).toEqual({ asks: 4, items: 2 });
    expect(badgeCount(inbox, [])).toBe(4);
    // Two of the item's three asks answered (even inside the undo window): the asks drop by two, the item stays.
    const two = answer(item, { kind: 'approve', numbers: [1, 2] }, 'undo');
    expect(inboxCounts(inbox, [two])).toEqual({ asks: 2, items: 2 });
    expect(badgeCount(inbox, [two])).toBe(2);
    // Every ask on one item answered: the items drop too.
    const all = answer(other, { kind: 'approve', numbers: [1] }, 'undo');
    expect(inboxCounts(inbox, [two, all])).toEqual({ asks: 1, items: 1 });
    // Nothing waits: no badge. Not loaded: no badge.
    expect(badgeCount(anInbox({ items: [] }), [])).toBeNull();
    expect(badgeCount(undefined, [])).toBeNull();
  });

  it('keeps an item with no asks out of the item drop, as the list keeps it', () => {
    const bare = anInboxItem({ asks: [] });
    const inbox = anInbox({ items: [item, bare], openAsks: 3, openItems: 2 });
    expect(visibleItems(inbox.items, [])).toEqual([item, bare]);
    expect(inboxCounts(inbox, [])).toEqual({ asks: 3, items: 2 });
  });

  it('writes "15 asks on 6 items" for the design data', () => {
    const inbox = designInbox('p1');
    const counts = inboxCounts(inbox, []);
    expect(waitingSubtitle(counts, true)).toBe('15 asks on 6 items');
    expect(visibleItems(inbox.items, [])).toHaveLength(6);
  });

  it('writes the singular, and the empty states', () => {
    expect(waitingSubtitle({ asks: 1, items: 1 }, true)).toBe('1 ask on 1 item');
    expect(waitingSubtitle({ asks: 0, items: 0 }, true)).toBe('nothing waiting');
    expect(waitingSubtitle({ asks: 0, items: 0 }, false)).toBeUndefined();
    expect(emptyKind(anInbox({ items: [] }), [])).toBe('clear');
    expect(emptyKind(anInbox({ items: [], everWaited: false }), [])).toBe('new');
    expect(emptyKind(anInbox(), anInbox().items)).toBeNull();
  });
});
