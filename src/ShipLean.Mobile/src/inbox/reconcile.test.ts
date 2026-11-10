import { describe, expect, it } from 'vitest';
import { aDecisionAsk, anInbox, anInboxItem } from '../test-support/inbox';
import type { InboxAction } from './actions';
import { reconcileAnswer } from './reconcile';

const approveOn = (itemId: string): InboxAction => ({
  area: 'inbox',
  projectId: 'project-1',
  itemId,
  key: 'SHIP-1',
  kind: 'approve',
  numbers: [1, 2],
});

describe('reconcile', () => {
  it('says notSent when one of the labels is still open', () => {
    const item = anInboxItem({ asks: [aDecisionAsk({ number: 2 }), aDecisionAsk({ number: 3 })] });
    // D2 of this item is open; another item's D1 being open says nothing about this one.
    const other = anInboxItem({ asks: [aDecisionAsk({ number: 1 })] });
    expect(reconcileAnswer(approveOn(item.id), anInbox({ items: [other, item] }))).toBe('notSent');
  });

  it('says sent when none of the labels is open', () => {
    const item = anInboxItem({ asks: [aDecisionAsk({ number: 3 })] });
    const other = anInboxItem({ asks: [aDecisionAsk({ number: 1 })] });
    expect(reconcileAnswer(approveOn(item.id), anInbox({ items: [other, item] }))).toBe('sent');
    expect(reconcileAnswer(approveOn(item.id), anInbox({ items: [] }))).toBe('sent');
  });
});
