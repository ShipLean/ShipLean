// The Inbox's actions (K7b, N8). Every answer from the Inbox, by a tap, typing or voice, is one of these, sent through the
// session's action runner. Every kind carries its project (the sender and the outbox need it).
import type { Pending } from '../actions/types';

export type InboxAction = { area: 'inbox'; projectId: string; itemId: string; key: string } & (
  | { kind: 'approve'; numbers: number[] }
  // Voice only. alternativeIndex null is the recommended answer.
  | { kind: 'chooseDecision'; number: number; alternativeIndex: number | null }
  | { kind: 'chooseOption'; number: number; optionIndex: number; option: string }
  | { kind: 'answer'; number: number; body: string }
  | { kind: 'discuss'; number: number; body: string }
  | {
      kind: 'review';
      confirm: number[];
      changes: {
        number: number;
        alternativeIndex: number | null;
        ownText: string | null;
        note: string | null;
      }[];
    }
  // label is the mention's ask label, "@1".
  | { kind: 'mentionReply'; mentionId: string; label: string; body: string }
  | { kind: 'seen'; mentionId: string; label: string }
  | { kind: 'stillTrue'; revision: number }
  // from is the line it replaces: Undo writes it back.
  | { kind: 'newStatus'; revision: number; text: string; from: string }
);

export type InboxKind = InboxAction['kind'];

// One of the runner's pendings for an Inbox action, with the ask labels it answers.
export type PendingAnswer = Pending & { action: InboxAction; labels: string[] };

const decision = (n: number) => `D${n}`;
const question = (n: number) => `Q${n}`;

// The labels of the asks an action answers, as the Inbox shows them: "D1", "Q2", "@1", "Status".
export function labelsOf(action: InboxAction): string[] {
  switch (action.kind) {
    case 'approve':
      return action.numbers.map(decision);
    case 'review':
      return [...new Set([...action.confirm, ...action.changes.map((c) => c.number)])]
        .sort((a, b) => a - b)
        .map(decision);
    case 'chooseDecision':
    case 'discuss':
      return [decision(action.number)];
    case 'chooseOption':
    case 'answer':
      return [question(action.number)];
    case 'mentionReply':
    case 'seen':
      return [action.label];
    case 'stillTrue':
    case 'newStatus':
      return ['Status'];
  }
}
