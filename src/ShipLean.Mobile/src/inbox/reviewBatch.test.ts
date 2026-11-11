import { describe, expect, it } from 'vitest';
import type { PendingState } from '../actions/types';
import { aPending } from '../test-support/actions';
import type { InboxAction } from './actions';
import { foldAnswers, toBatch, type InboxPending } from './reviewBatch';

const base = { area: 'inbox', projectId: 'project-1', itemId: 'item-1', key: 'SHIP-1' } as const;

const c3 = { number: 3, alternativeIndex: 0, ownText: null, note: null };
const c4 = {
  number: 4,
  alternativeIndex: null,
  ownText: 'Keep it in the profile',
  note: 'Cheaper',
};
const c5 = { number: 5, alternativeIndex: 1, ownText: null, note: null };

const review: InboxAction = {
  ...base,
  kind: 'review',
  confirm: [1, 2],
  changes: [c3, c4, c5],
};

const asPending = (parts: InboxAction[], states: PendingState[]): InboxPending[] =>
  parts.map((action, i) => ({
    ...aPending({ id: `p${i + 1}`, batchId: 'batch-1', state: states[i]! }),
    action,
  }));

describe('review batches', () => {
  it('splits a review into the confirm then one change each', () => {
    expect(toBatch(review)).toEqual([
      { ...base, kind: 'review', confirm: [1, 2], changes: [] },
      { ...base, kind: 'review', confirm: [], changes: [c3] },
      { ...base, kind: 'review', confirm: [], changes: [c4] },
      { ...base, kind: 'review', confirm: [], changes: [c5] },
    ]);
    const approve: InboxAction = { ...base, kind: 'approve', numbers: [7] };
    expect(toBatch(approve)).toEqual([approve]);
  });

  it('folds a batch that failed at its second change into what is left', () => {
    const parts = asPending(toBatch(review), ['sent', 'sent', 'failed', 'failed']);
    parts[2] = { ...parts[2]!, error: 'Nothing changed.', failure: 'server', retryable: true };

    const answers = foldAnswers(parts);

    expect(answers).toHaveLength(1);
    expect(answers[0]).toMatchObject({
      id: 'p3',
      batchId: 'batch-1',
      state: 'failed',
      error: 'Nothing changed.',
      retryable: true,
      labels: ['D4', 'D5'],
      action: {
        kind: 'review',
        confirm: [],
        changes: [c4, c5],
      },
    });
  });

  it('folds a fully sent batch to sent', () => {
    const approve: InboxAction = { ...base, itemId: 'item-2', kind: 'approve', numbers: [9] };
    const parts = [
      ...asPending(toBatch(review), ['sent', 'sent', 'sent', 'sent']),
      { ...aPending({ id: 'p9', batchId: 'batch-2', state: 'undo' }), action: approve },
    ];

    const answers = foldAnswers(parts);

    expect(answers.map((a) => [a.id, a.state, a.labels])).toEqual([
      ['p1', 'sent', ['D1', 'D2', 'D3', 'D4', 'D5']],
      ['p9', 'undo', ['D9']],
    ]);
    expect(answers[0]!.action).toEqual(review);
  });
});
