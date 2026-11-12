import { describe, expect, it } from 'vitest';
import { testedLabel } from './inputLabels';

const now = new Date('2026-11-12T12:00:00Z');

describe('testedLabel', () => {
  it('reads today, yesterday, days ago and the date', () => {
    expect(testedLabel('2026-11-12T09:00:00Z', now)).toBe('today');
    expect(testedLabel('2026-11-11T12:00:00Z', now)).toBe('yesterday');
    expect(testedLabel('2026-11-09T12:00:00Z', now)).toBe('3 days ago');
    expect(testedLabel('2026-10-01T12:00:00Z', now)).toBe('2026-10-01');
  });
  it('is null when never tested', () => {
    expect(testedLabel('', now)).toBeNull();
  });
});
