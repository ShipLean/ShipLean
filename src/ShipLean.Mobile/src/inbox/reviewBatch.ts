// A review sent as a batch, and a batch read back as the answers the Inbox shows (K7b). A review is split into its
// confirm, then one change each, so the runner stops at the first failure and the rest stay unsent; the Inbox still
// shows it as one answer, holding only what wasn't sent.
import type { Pending, PendingState } from '../actions/types';
import { labelsOf, type InboxAction, type PendingAnswer } from './actions';

export type InboxPending = Pending & { action: InboxAction };

type Review = Extract<InboxAction, { kind: 'review' }>;
type ReviewPending = Pending & { action: Review };

const isReview = (p: InboxPending): p is ReviewPending => p.action.kind === 'review';

// The batch one Inbox answer is sent as: a review becomes its confirm (when it confirms any) then one review per change,
// in order; every other answer is a batch of one.
export function toBatch(a: InboxAction): InboxAction[] {
  if (a.kind !== 'review') return [a];
  const confirm: Review[] = a.confirm.length > 0 ? [{ ...a, changes: [] }] : [];
  return [
    ...confirm,
    ...a.changes.map((change): Review => ({ ...a, confirm: [], changes: [change] })),
  ];
}

// Worst first: the answer shows the state of its worst part.
const WORST: PendingState[] = ['failed', 'uncertain', 'sending', 'queued', 'undo', 'sent'];

function foldReview(parts: ReviewPending[]): PendingAnswer {
  const left = parts.filter((p) => p.state !== 'sent');
  const shown = left.length > 0 ? left : parts;
  const action: Review = {
    ...parts[0]!.action,
    confirm: shown.flatMap((p) => p.action.confirm),
    changes: shown.flatMap((p) => p.action.changes),
  };
  const worst = WORST.find((s) => parts.some((p) => p.state === s));
  const worstPart = parts.find((p) => p.state === worst) ?? parts[0]!;
  return { ...worstPart, action, labels: labelsOf(action) };
}

// The parts of the same review as p (same batch, same item), p included, in the runner's order.
export function partsOf(p: InboxPending, pending: InboxPending[]): InboxPending[] {
  if (!isReview(p)) return [p];
  return pending.filter(
    (q) => isReview(q) && q.batchId === p.batchId && q.action.itemId === p.action.itemId,
  );
}

// The runner's Inbox pendings as the Inbox's answers, in the runner's order: each review batch folds into one answer
// whose action is what is left to send (the whole review once all of it is sent) and whose state is its worst part's.
export function foldAnswers(pending: InboxPending[]): PendingAnswer[] {
  const answers: PendingAnswer[] = [];
  const folded = new Set<InboxPending>();
  for (const p of pending) {
    if (folded.has(p)) continue;
    if (!isReview(p)) {
      answers.push({ ...p, labels: labelsOf(p.action) });
      continue;
    }
    const parts = partsOf(p, pending) as ReviewPending[];
    for (const q of parts) folded.add(q);
    answers.push(foldReview(parts));
  }
  return answers;
}
