import { describe, expect, it } from 'vitest';
import { rebase } from './rebase';

describe('rebase', () => {
  it('rebase_WhenSectionChangedSinceSent_ShouldKeepItAndTakeTheRestFromReturned', () => {
    const sent = { a: 'x', b: 'y', c: 'z' };
    const latest = { a: 'x2', b: 'y', c: 'z' };
    const returned = { a: 'x', b: 'theirs', c: 'z' };
    expect(rebase(sent, latest, returned)).toEqual({ a: 'x2', b: 'theirs', c: 'z' });
  });
});
