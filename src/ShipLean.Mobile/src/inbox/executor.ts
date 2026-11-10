import type { Executor, PreviewLine } from '../actions/types';
import type { InboxAction, InboxKind } from './actions';

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

export const inboxExecutor: Executor<InboxAction> = {
  area: 'inbox',
  // TODO: the Inbox senders (asks, mention and status writes through @shiplean/core), undo and reconcile replace this.
  send() {
    throw new Error('The Inbox executor does not send yet.');
  },
  // TODO: the Inbox wording replaces this line.
  describe(a): PreviewLine {
    return { id: `${a.kind}:${a.key}`, icon: 'check', text: a.key, removable: true };
  },
  reversible: (kind) => REVERSIBLE[kind],
};
