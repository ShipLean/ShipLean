import { describe, expect, it } from 'vitest';
import { aDecisionAsk, anInboxItem } from '../../test-support/inbox';
import {
  canConfirm,
  choose,
  decisionLabels,
  initialReview,
  openDecisions,
  reviewAction,
  reviewLead,
  reviewNote,
  reviewTitle,
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
    s = setNote(s, 2, '  two steps  ');
    expect(build(s)).toMatchObject({
      confirm: [1, 3, 4],
      changes: [{ number: 2, alternativeIndex: null, ownText: 'Both, in turn', note: 'two steps' }],
    });
    expect(build(setNote(s, 2, '   '))).toMatchObject({ changes: [{ number: 2, note: null }] });
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

describe('review copy', () => {
  it('names the changed decisions in the note and drops the clause when none changed', () => {
    const labels = decisionLabels(item);
    expect(reviewNote(item, labels, initialReview(decisions))).toBe(
      'Posted on SHIP-907 as your answers: 4 recommended. The next agent to pick up SHIP-907 reads them.',
    );
    let s = choose(initialReview(decisions), 3, { kind: 'alternative', index: 0 });
    s = choose(s, 1, { kind: 'own', text: 'x' });
    expect(reviewNote(item, labels, s)).toBe(
      'Posted on SHIP-907 as your answers: 2 recommended, D1, D3 changed. The next agent to pick up SHIP-907 reads them.',
    );
  });

  it('builds the lead from the first decision ask', () => {
    const lead = anInboxItem({
      title: 'Phone app',
      asks: [aDecisionAsk({ number: 1 }, { agentName: 'Claude Code', atLabel: 'yesterday' })],
    });
    expect(reviewLead(lead)).toBe('Phone app · from Claude Code, yesterday.');
  });

  it('titles a contiguous run by its ids and falls back when numbers skip', () => {
    expect(reviewTitle(item, decisionLabels(item))).toBe('Confirm D1–D4 on SHIP-907');
    const gap = anInboxItem({
      key: 'SHIP-5',
      asks: [1, 3, 5].map((number) => aDecisionAsk({ number })),
    });
    expect(reviewTitle(gap, decisionLabels(gap))).toBe('Confirm 3 decisions on SHIP-5');
  });
});
