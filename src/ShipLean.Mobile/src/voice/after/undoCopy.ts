// The words after Apply and Undo (voice D16, D18, D21), and which decisions a change that stayed can be changed on.
import type { AppAction, UndoResult } from '../../actions/types';

// What a kind with no reverse is called in "… can't be taken back here" (N8's irreversible kinds).
const NAMES: Record<string, string> = {
  approve: 'An approval',
  chooseDecision: 'A decision answer',
  review: 'A review',
  change: 'A decision change',
  answer: 'An answer',
  chooseOption: 'An answer',
  discuss: 'A comment on a decision',
  mentionReply: 'A reply',
  seen: 'Marking a mention seen',
  newAction: 'A retro action item',
  addToAction: 'A retro action item',
};

// Why a change stayed when its batch was undone.
export function whyStayed(kind: string, outcome: Exclude<UndoResult, 'undone'>): string {
  if (outcome === 'failed') return 'Couldn’t reach ShipLean, so this one wasn’t undone.';
  return `${NAMES[kind] ?? 'This change'} can’t be taken back here.`;
}

// The decision numbers an action answered, which Change D<n> can change instead of undoing.
export function decisionNumbers(action: AppAction): number[] {
  const a = action as AppAction & { numbers?: unknown; number?: unknown };
  if (a.kind === 'approve' && Array.isArray(a.numbers)) return a.numbers as number[];
  if (a.kind === 'chooseDecision' && typeof a.number === 'number') return [a.number];
  return [];
}

export const UNDO_COPY = {
  undone: (key: string | null) => (key === null ? 'Undone' : `Undone · ${key} is as it was`),
  previewTitle: (n: number) => (n === 1 ? 'Undo 1 change' : `Undo ${n} changes`),
  failedTitle: 'Couldn’t undo: no connection',
  failedDetail: 'Nothing was undone. Try again when you’re back online.',
  partialTitle: (undone: number, all: number) => `Undid ${undone} of ${all}`,
  stayedHeading: 'What stayed',
  undoneHeading: 'Undone',
  changeHint:
    'Change the answer instead: it’s posted on the item, and the next agent to pick it up reads the new answer.',
  posted: (key: string) => `Posted on ${key}; the next agent to pick it up reads it.`,
  changeFailed: 'Couldn’t change it. Try again.',
  rowTitle: 'Your voice change',
  waiting: 'Waiting to send',
  changedSince: 'Someone else changed it since, so it can’t be undone here.',
  refusedTitle: (refused: number, all: number) =>
    all === 1
      ? 'Your voice change wasn’t applied'
      : `${refused} of ${all} voice changes ${refused === 1 ? 'wasn’t' : 'weren’t'} applied`,
  refusedAllTitle: (n: number) =>
    n === 1 ? '1 voice change wasn’t applied' : `${n} voice changes weren’t applied`,
  wentThrough: (texts: string[]) => `${texts.join(', ')} went through.`,
} as const;
