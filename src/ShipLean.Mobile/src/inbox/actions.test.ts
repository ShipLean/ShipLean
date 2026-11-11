import { describe, expect, it } from 'vitest';
import { labelsOf, type InboxAction } from './actions';

const on = { area: 'inbox', projectId: 'p-1', itemId: 'item-1', key: 'SHIP-1' } as const;

describe('labelsOf', () => {
  it('labels approve [1..8] as D1…D8', () => {
    expect(labelsOf({ ...on, kind: 'approve', numbers: [1, 2, 3, 4, 5, 6, 7, 8] })).toEqual([
      'D1',
      'D2',
      'D3',
      'D4',
      'D5',
      'D6',
      'D7',
      'D8',
    ]);
  });

  it('labels a review by its confirmed and changed numbers', () => {
    const review: InboxAction = {
      ...on,
      kind: 'review',
      confirm: [1, 2, 3, 5],
      changes: [{ number: 4, alternativeIndex: 0, ownText: null, note: 'Cheaper.' }],
    };
    expect(labelsOf(review)).toEqual(['D1', 'D2', 'D3', 'D4', 'D5']);
  });

  it('labels seen and mentionReply by @n, stillTrue and newStatus as Status', () => {
    expect(labelsOf({ ...on, kind: 'seen', mentionId: 'm-2', label: '@2' })).toEqual(['@2']);
    expect(
      labelsOf({ ...on, kind: 'mentionReply', mentionId: 'm-1', label: '@1', body: 'Done.' }),
    ).toEqual(['@1']);
    expect(labelsOf({ ...on, kind: 'stillTrue', revision: 3 })).toEqual(['Status']);
    expect(
      labelsOf({ ...on, kind: 'newStatus', revision: 3, text: 'Shipped.', from: 'Blocked.' }),
    ).toEqual(['Status']);
  });

  it('labels chooseOption and answer as Qn, chooseDecision and discuss as Dn', () => {
    expect(
      labelsOf({ ...on, kind: 'chooseOption', number: 7, optionIndex: 1, option: '50 MB' }),
    ).toEqual(['Q7']);
    expect(labelsOf({ ...on, kind: 'answer', number: 2, body: 'Yes.' })).toEqual(['Q2']);
    expect(labelsOf({ ...on, kind: 'chooseDecision', number: 10, alternativeIndex: null })).toEqual(
      ['D10'],
    );
    expect(labelsOf({ ...on, kind: 'discuss', number: 11, body: 'Why?' })).toEqual(['D11']);
  });
});
