import { describe, expect, it } from 'vitest';
import { chooseModel, MODEL_FILES, soldGb } from './manifest';

describe('manifest', () => {
  it('rounds reported memory up to the size sold', () => {
    expect(soldGb(11.2)).toBe(12);
    expect(soldGb(7.6)).toBe(8);
    expect(soldGb(5.6)).toBe(6);
    expect(soldGb(8)).toBe(8);
    expect(soldGb(3.7)).toBe(4);
  });

  it('chooses 4B at 11 GB reported, 1.7B with a warning at 8, 1.7B only at 6 and nothing below', () => {
    expect(chooseModel(11)).toEqual({ auto: 'qwen3-4b', fourB: 'ok', reason: null });
    expect(chooseModel(15.1)).toEqual({ auto: 'qwen3-4b', fourB: 'ok', reason: null });
    expect(chooseModel(10.9)).toEqual({ auto: 'qwen3-1.7b', fourB: 'warn', reason: null });
    expect(chooseModel(7.6)).toEqual({ auto: 'qwen3-1.7b', fourB: 'warn', reason: null });
    expect(chooseModel(5.6)).toEqual({ auto: 'qwen3-1.7b', fourB: 'blocked', reason: null });
    expect(chooseModel(3.7)).toEqual({ auto: null, fourB: 'blocked', reason: 'lowMemory' });
  });

  it('lists one file per model with its licence', () => {
    expect(MODEL_FILES.map((f) => [f.model, f.licence])).toEqual([
      ['parakeet', 'CC-BY-4.0'],
      ['qwen3-1.7b', 'Apache-2.0'],
      ['qwen3-4b', 'Apache-2.0'],
    ]);
  });
});
