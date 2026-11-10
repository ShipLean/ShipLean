import { describe, expect, it } from 'vitest';
import { closestFromSearch, closestRows, spanOf } from './closest';

const rows = [
  { key: 'SHIP-1', title: 'Onboarding emails' },
  { key: 'SHIP-2', title: 'Set up the onboarding story' },
  { key: 'SHIP-3', title: 'Billing' },
  { key: 'SHIP-4', title: 'Onboarding story copy' },
];

describe('closestRows', () => {
  it('returns the two rows sharing the most title words, best first', () => {
    expect(closestRows('the onboarding story', rows).map((r) => r.key)).toEqual([
      'SHIP-2',
      'SHIP-4',
    ]);
  });
  it('leaves out rows that share no word, and never returns more than two', () => {
    expect(closestRows('billing', rows).map((r) => r.key)).toEqual(['SHIP-3']);
    expect(closestRows('onboarding', rows)).toHaveLength(2);
    expect(closestRows('zzz', rows)).toEqual([]);
  });
});

describe('closestFromSearch', () => {
  it('keeps search order, to this project, at most two', () => {
    const items = [
      { key: 'PAY-1', title: 'a', projectKey: 'PAY' },
      { key: 'SHIP-1', title: 'b', projectKey: 'SHIP' },
      { key: 'SHIP-2', title: 'c', projectKey: 'SHIP' },
      { key: 'SHIP-3', title: 'd', projectKey: 'SHIP' },
    ];
    expect(closestFromSearch(items, 'SHIP').map((r) => r.key)).toEqual(['SHIP-1', 'SHIP-2']);
  });
});

describe('spanOf', () => {
  it('finds the spoken words in the heard text, ignoring case', () => {
    expect(spanOf('Move SHIP-960 up', 'ship-960')).toEqual({ start: 5, end: 13 });
  });
  it('is null when the words are not in the text or not given', () => {
    expect(spanOf('Move it', 'ship-960')).toBeNull();
    expect(spanOf('Move it', undefined)).toBeNull();
  });
});
