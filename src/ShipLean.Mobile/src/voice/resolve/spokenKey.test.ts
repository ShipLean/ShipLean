import { describe, expect, it } from 'vitest';
import { readSpokenKey } from './spokenKey';

describe('spokenKey', () => {
  it('reads "ship nine oh six", "ship 906", "SHIP906" and "ship dash nine zero six" as SHIP-906', () => {
    expect(readSpokenKey('ship nine oh six')).toBe('SHIP-906');
    expect(readSpokenKey('ship 906')).toBe('SHIP-906');
    expect(readSpokenKey('SHIP906')).toBe('SHIP-906');
    expect(readSpokenKey('ship dash nine zero six')).toBe('SHIP-906');
  });
  it('reads pairs, teens and hundreds the way keys are said', () => {
    expect(readSpokenKey('ship nine sixty')).toBe('SHIP-960');
    expect(readSpokenKey('ship ten ninety one')).toBe('SHIP-1091');
    expect(readSpokenKey('ship thirteen')).toBe('SHIP-13');
    expect(readSpokenKey('ship nine hundred and six')).toBe('SHIP-906');
    expect(readSpokenKey('Ship-1091')).toBe('SHIP-1091');
  });
  it('reads nothing without a key prefix, without a number, or with other words in the number', () => {
    expect(readSpokenKey('nine oh six')).toBeNull();
    expect(readSpokenKey('ship')).toBeNull();
    expect(readSpokenKey('ship nine to done')).toBeNull();
    expect(readSpokenKey('')).toBeNull();
  });
});
