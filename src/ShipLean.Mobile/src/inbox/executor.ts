import {
  answerQuestion,
  changeDecision,
  confirmDecisions,
  discussDecision,
} from '@shiplean/core/api/asks';
import { getInbox, markMentionSeen, replyToMention } from '@shiplean/core/api/inbox';
import { confirmStatus, setStatusLine } from '@shiplean/core/api/status';
import { getItemByKey } from '@shiplean/core/api/workItem';
import type { AppAction, Executor, PreviewLine } from '../actions/types';
import { labelsOf, type InboxAction, type InboxKind } from './actions';
import { reconcileAnswer } from './reconcile';
import { compressLabels } from './rowModel';

// Only a new status line has a reverse (the previous line is written back). Every other answer is posted on the item
// and stays (decisions A.2).
const REVERSIBLE: Record<InboxKind, boolean> = {
  approve: false,
  chooseDecision: false,
  chooseOption: false,
  answer: false,
  discuss: false,
  review: false,
  mentionReply: false,
  seen: false,
  stillTrue: false,
  newStatus: true,
};

function asInbox(a: AppAction): InboxAction {
  if (a.area !== 'inbox') throw new Error(`The Inbox executor was given a ${a.area} action.`);
  return a as InboxAction;
}

async function send(a: InboxAction): Promise<void> {
  switch (a.kind) {
    case 'approve':
      return confirmDecisions(a.itemId, a.numbers);
    case 'chooseDecision':
      // A null index is the recommended answer: confirming it.
      if (a.alternativeIndex === null) return confirmDecisions(a.itemId, [a.number]);
      return changeDecision(a.itemId, a.number, {
        kind: 'alternative',
        alternativeIndex: a.alternativeIndex,
        text: null,
        note: null,
      });
    case 'chooseOption':
      return answerQuestion(a.itemId, a.number, a.option);
    case 'answer':
      return answerQuestion(a.itemId, a.number, a.body);
    case 'discuss':
      return discussDecision(a.itemId, a.number, a.body);
    case 'review':
      if (a.confirm.length > 0) await confirmDecisions(a.itemId, a.confirm);
      for (const c of a.changes)
        await changeDecision(a.itemId, c.number, {
          kind: c.ownText !== null ? 'own' : 'alternative',
          alternativeIndex: c.alternativeIndex,
          text: c.ownText,
          note: c.note,
        });
      return;
    case 'mentionReply':
      return replyToMention(a.mentionId, a.body);
    case 'seen':
      return markMentionSeen(a.mentionId);
    case 'stillTrue':
      await confirmStatus(a.itemId, a.revision);
      return;
    case 'newStatus':
      await setStatusLine(a.itemId, a.revision, a.text);
      return;
  }
}

function wording(a: InboxAction): { icon: string; text: string } {
  const labels = compressLabels(labelsOf(a));
  switch (a.kind) {
    case 'approve':
      return {
        icon: 'check',
        text: `${labels} ${a.numbers.length === 1 ? 'approved' : 'confirmed'}`,
      };
    case 'chooseDecision':
      return {
        icon: 'check',
        text: `${labels} ${a.alternativeIndex === null ? 'approved' : 'changed'}`,
      };
    case 'review':
      return {
        icon: 'check',
        text: `${labels} ${a.changes.length === 0 ? 'confirmed' : 'decided'}`,
      };
    case 'chooseOption':
    case 'answer':
      return { icon: 'note', text: `Answered ${labels}` };
    case 'discuss':
      return { icon: 'note', text: `Discussed ${labels}` };
    case 'mentionReply':
      return { icon: 'note', text: `Replied to ${a.label}` };
    case 'seen':
      return { icon: 'check', text: `${a.label} seen` };
    case 'stillTrue':
      return { icon: 'check', text: 'Status still true' };
    case 'newStatus':
      return { icon: 'edit', text: 'New status' };
  }
}

export const inboxExecutor: Executor<InboxAction> = {
  area: 'inbox',
  // Every Inbox send resolves void: nothing the server creates is needed for Undo.
  send: (a) => send(a),
  describe(a): PreviewLine {
    const { icon, text } = wording(a);
    return {
      id: `inbox:${a.itemId}:${a.kind}:${labelsOf(a).join(',')}`,
      icon,
      text,
      removable: true,
    };
  },
  reversible: (kind) => REVERSIBLE[kind],
  // Only newStatus is reversible. The send moved the item's revision, so read the item now: if its line is no longer
  // the one sent, someone changed it since and Undo fails without writing; otherwise write the previous line back at
  // the current revision (a 409 fails too).
  async undo(a) {
    if (a.kind !== 'newStatus') throw new Error(`An Inbox ${a.kind} has no reverse.`);
    const item = await getItemByKey(a.projectId, a.key);
    if (item.statusLine !== a.text) throw new Error('The status line has changed since.');
    await setStatusLine(a.itemId, item.revision, a.from);
  },
  // P5: one fresh Inbox per project; an answer whose asks are all closed went through, otherwise it is sent again.
  async reconcile(items) {
    const actions = items.map((p) => asInbox(p.action));
    const projectIds = [...new Set(actions.map((a) => a.projectId))];
    const inboxes = new Map(
      await Promise.all(projectIds.map(async (p) => [p, await getInbox(p)] as const)),
    );
    return actions.map((a) => reconcileAnswer(a, inboxes.get(a.projectId)!));
  },
};
