import { describe, expect, it } from 'vitest';
import { reasonLine } from './reasonLine';

describe('reasonLine', () => {
  it('micOff and micCheck give the typing-instead line; edit gives none', () => {
    expect(reasonLine('micOff')).toBe('Microphone is off · typing instead');
    expect(reasonLine('micCheck')).toBe('Microphone is off · typing instead');
    expect(reasonLine('edit')).toBeNull();
    expect(reasonLine(null)).toBeNull();
  });
});
