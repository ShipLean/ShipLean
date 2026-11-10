// The review sheet's state and the one `review` action it becomes (K7b). Pure: no React.
import type { InboxDecisionDto, InboxItemDto } from '@shiplean/core/api/inbox';
import type { InboxAction } from '../actions';

export type ReviewChoice =
  | { kind: 'recommended' }
  | { kind: 'alternative'; index: number }
  // An own answer is free text.
  | { kind: 'own'; text: string };

export interface ReviewDecisionState {
  choice: ReviewChoice;
  // Typed "Add why". Only a changed decision carries it.
  note: string;
}

// By decision number.
export type ReviewState = ReadonlyMap<number, ReviewDecisionState>;

export const RECOMMENDED: ReviewDecisionState = { choice: { kind: 'recommended' }, note: '' };

// Every open decision on the item, in the order the Inbox lists them.
export function openDecisions(item: InboxItemDto): InboxDecisionDto[] {
  return item.asks.flatMap((a) =>
    a.kind === 'decision' && a.decision !== null ? [a.decision] : [],
  );
}

// Every decision starts on its recommended answer.
export function initialReview(decisions: InboxDecisionDto[]): ReviewState {
  return new Map(decisions.map((d) => [d.number, RECOMMENDED]));
}

export function isChanged(s: ReviewDecisionState): boolean {
  return s.choice.kind !== 'recommended';
}

// Choosing the recommended answer again drops the note: only a changed decision has a reason.
export function choose(state: ReviewState, number: number, choice: ReviewChoice): ReviewState {
  const next = new Map(state);
  const current = state.get(number) ?? RECOMMENDED;
  next.set(number, { choice, note: choice.kind === 'recommended' ? '' : current.note });
  return next;
}

export function setNote(state: ReviewState, number: number, note: string): ReviewState {
  const next = new Map(state);
  const current = state.get(number) ?? RECOMMENDED;
  next.set(number, { ...current, note });
  return next;
}

// An own answer needs text before Confirm.
export function canConfirm(state: ReviewState): boolean {
  return [...state.values()].every((s) => s.choice.kind !== 'own' || s.choice.text.trim() !== '');
}

export function changedCount(state: ReviewState): number {
  return [...state.values()].filter(isChanged).length;
}

// One review: unchanged decisions confirm, changed ones carry their choice and note.
export function reviewAction(
  item: InboxItemDto,
  projectId: string,
  decisions: InboxDecisionDto[],
  state: ReviewState,
): InboxAction {
  const confirm: number[] = [];
  const changes: Extract<InboxAction, { kind: 'review' }>['changes'] = [];
  for (const d of decisions) {
    const s = state.get(d.number) ?? RECOMMENDED;
    if (s.choice.kind === 'recommended') {
      confirm.push(d.number);
      continue;
    }
    const note = s.note.trim();
    changes.push({
      number: d.number,
      alternativeIndex: s.choice.kind === 'alternative' ? s.choice.index : null,
      ownText: s.choice.kind === 'own' ? s.choice.text.trim() : null,
      note: note === '' ? null : note,
    });
  }
  return {
    area: 'inbox',
    projectId,
    itemId: item.id,
    key: item.key,
    kind: 'review',
    confirm,
    changes,
  };
}
