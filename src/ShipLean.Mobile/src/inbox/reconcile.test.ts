import { describe, expect, it } from 'vitest';
import { aPending } from '../test-support/actions';
import { aDecisionAsk, aMentionAsk, anInbox, anInboxItem, aUser } from '../test-support/inbox';
import { aComment } from '../test-support/workItem';
import type { InboxAction } from './actions';
import { reconcileAnswer, type DiscussionCheck } from './reconcile';

const approveOn = (itemId: string): InboxAction => ({
  area: 'inbox',
  projectId: 'project-1',
  itemId,
  key: 'SHIP-1',
  kind: 'approve',
  numbers: [1, 2],
});

const pendingOf = (action: InboxAction, createdAt = '2030-01-01T09:00:00.000Z') =>
  aPending({ action, createdAt, state: 'uncertain' }) as Parameters<typeof reconcileAnswer>[0];

const noDiscussion: DiscussionCheck = { callerId: 'me', comments: [] };

describe('reconcile', () => {
  it('says notSent when one of the labels is still open', () => {
    const item = anInboxItem({ asks: [aDecisionAsk({ number: 2 }), aDecisionAsk({ number: 3 })] });
    // D2 of this item is open; another item's D1 being open says nothing about this one.
    const other = anInboxItem({ asks: [aDecisionAsk({ number: 1 })] });
    expect(
      reconcileAnswer(
        pendingOf(approveOn(item.id)),
        anInbox({ items: [other, item] }),
        noDiscussion,
      ),
    ).toBe('notSent');
  });

  it('says sent when none of the labels is open', () => {
    const item = anInboxItem({ asks: [aDecisionAsk({ number: 3 })] });
    const other = anInboxItem({ asks: [aDecisionAsk({ number: 1 })] });
    const p = pendingOf(approveOn(item.id));
    expect(reconcileAnswer(p, anInbox({ items: [other, item] }), noDiscussion)).toBe('sent');
    expect(reconcileAnswer(p, anInbox({ items: [] }), noDiscussion)).toBe('sent');
  });

  describe('a discussion', () => {
    const item = anInboxItem({ asks: [aDecisionAsk({ number: 4 })] });
    // A discussion never closes its decision, so D4 stays open either way.
    const inbox = anInbox({ items: [item] });
    const me = aUser();
    const discuss: InboxAction = {
      area: 'inbox',
      projectId: 'project-1',
      itemId: item.id,
      key: 'SHIP-1',
      kind: 'discuss',
      number: 4,
      body: 'Why not the profile?',
    };
    const p = pendingOf(discuss);
    const commentAt = (createdAt: string, over = {}) =>
      aComment(item.id, { author: me, body: discuss.body, createdAt, ...over });

    it("says sent when the caller's comment with that body is on the item", () => {
      const comments = [commentAt('2030-01-01T09:00:05.000Z')];
      expect(reconcileAnswer(p, inbox, { callerId: me.id, comments })).toBe('sent');
    });

    it('says notSent when the comment is absent', () => {
      expect(reconcileAnswer(p, inbox, { callerId: me.id, comments: [] })).toBe('notSent');
    });

    it('says notSent for another author, another body, or a comment from before the answer', () => {
      const comments = [
        commentAt('2030-01-01T09:00:05.000Z', { author: aUser('Other', 'OT') }),
        commentAt('2030-01-01T09:00:05.000Z', { body: 'Something else' }),
        commentAt('2030-01-01T08:50:00.000Z'),
      ];
      expect(reconcileAnswer(p, inbox, { callerId: me.id, comments })).toBe('notSent');
    });

    it('allows two minutes between the device clock and the server clock', () => {
      const early = [commentAt('2030-01-01T08:58:30.000Z')];
      expect(reconcileAnswer(p, inbox, { callerId: me.id, comments: early })).toBe('sent');
    });
  });

  describe('a mention', () => {
    // The reply to the old @1 landed; the old @2 is now numbered "@1".
    const replied = aMentionAsk({ mentionId: 'mention-1' });
    const other = aMentionAsk({ mentionId: 'mention-2' }, { label: '@1' });
    const reply = (kind: 'mentionReply' | 'seen'): InboxAction =>
      kind === 'seen'
        ? { ...base(), kind, mentionId: 'mention-1', label: '@1' }
        : { ...base(), kind, mentionId: 'mention-1', label: '@1', body: 'On it' };
    function base() {
      return { area: 'inbox', projectId: 'project-1', itemId: item.id, key: 'SHIP-1' } as const;
    }
    const item = anInboxItem({ asks: [other] });
    const stillOpen = anInboxItem({ id: item.id, asks: [replied, other] });

    it.each(['mentionReply', 'seen'] as const)(
      'says sent for a %s whose mention is gone though another mention carries its label',
      (kind) => {
        expect(
          reconcileAnswer(pendingOf(reply(kind)), anInbox({ items: [item] }), noDiscussion),
        ).toBe('sent');
      },
    );

    it.each(['mentionReply', 'seen'] as const)(
      'says notSent for a %s whose mention is still open',
      (kind) => {
        expect(
          reconcileAnswer(pendingOf(reply(kind)), anInbox({ items: [stillOpen] }), noDiscussion),
        ).toBe('notSent');
      },
    );
  });
});
