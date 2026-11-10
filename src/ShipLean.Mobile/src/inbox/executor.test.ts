import { describe, expect, it } from 'vitest';
import type { InboxKind } from './actions';
import { inboxExecutor } from './executor';

const KINDS: InboxKind[] = [
  'approve',
  'chooseDecision',
  'chooseOption',
  'answer',
  'discuss',
  'review',
  'mentionReply',
  'seen',
  'stillTrue',
  'newStatus',
];

describe('inboxExecutor', () => {
  it('marks only newStatus reversible', () => {
    expect(inboxExecutor.area).toBe('inbox');
    expect(KINDS.filter((k) => inboxExecutor.reversible(k))).toEqual(['newStatus']);
  });
});
