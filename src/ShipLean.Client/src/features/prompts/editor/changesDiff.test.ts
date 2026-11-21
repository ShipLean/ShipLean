import { describe, expect, it } from 'vitest';
import { changesFromBuiltIn, countChanges } from './changesDiff';

describe('changesFromBuiltIn', () => {
  it('has no hunks when the text is the built-in', () => {
    expect(changesFromBuiltIn('a\nb\n', 'a\nb\n')).toEqual([]);
  });

  it('reports an added line and a removed line', () => {
    const hunks = changesFromBuiltIn('a\nb\nc\n', 'a\nB\nc\nd\n');
    expect(countChanges(hunks)).toEqual({ adds: 2, dels: 1 });
    expect(hunks.flatMap((h) => h.lines).find((l) => l.kind === 'del')?.text).toBe('b');
  });

  it('ignores a CRLF difference', () => {
    expect(changesFromBuiltIn('a\r\nb\r\n', 'a\nb\n')).toEqual([]);
  });
});
