import { describe, expect, it } from 'vitest';
import { aDecisionAsk, anInboxItem } from '../../test-support/inbox';
import {
  canConfirm,
  choose,
  initialReview,
  openDecisions,
  reviewAction,
  setNote,
} from './reviewModel';

const item = anInboxItem({
  key: 'SHIP-907',
  asks: [1, 2, 3, 4].map((number) => aDecisionAsk({ number, alternatives: ['Alt A', 'Alt B'] })),
});
const decisions = openDecisions(item);
const build = (state = initialReview(decisions)) => reviewAction(item, 'p1', decisions, state);

describe('review model', () => {
  it('confirms every decision when nothing is changed', () => {
    expect(build()).toEqual({
      area: 'inbox',
      projectId: 'p1',
      itemId: item.id,
      key: 'SHIP-907',
      kind: 'review',
      confirm: [1, 2, 3, 4],
      changes: [],
    });
  });

  it('moves a decision changed to an alternative into changes', () => {
    let s = choose(initialReview(decisions), 4, { kind: 'alternative', index: 1 });
    s = setNote(s, 4, '  cheaper  ');
    const a = build(s);
    expect(a).toMatchObject({
      confirm: [1, 2, 3],
      changes: [{ number: 4, alternativeIndex: 1, ownText: null, note: 'cheaper' }],
    });
  });

  it('carries an own answer and its note', () => {
    let s = choose(initialReview(decisions), 2, { kind: 'own', text: ' Both, in turn ' });
    s = setNote(s, 2, '');
    expect(build(s)).toMatchObject({
      confirm: [1, 3, 4],
      changes: [{ number: 2, alternativeIndex: null, ownText: 'Both, in turn', note: null }],
    });
    expect(canConfirm(choose(s, 2, { kind: 'own', text: '  ' }))).toBe(false);
    expect(canConfirm(s)).toBe(true);
  });

  it('puts a decision changed back to recommended into confirm', () => {
    let s = choose(initialReview(decisions), 3, { kind: 'alternative', index: 0 });
    s = setNote(s, 3, 'because');
    s = choose(s, 3, { kind: 'recommended' });
    expect(build(s)).toMatchObject({ confirm: [1, 2, 3, 4], changes: [] });
  });
});
