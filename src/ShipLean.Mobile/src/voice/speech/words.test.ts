import { describe, expect, it } from 'vitest';
import { LAST_TOKEN_MS, toWords } from './words';

describe('words', () => {
  it('token timestamps become words with startMs and endMs', () => {
    // What sherpa-onnx 1.12.35 returned for Parakeet TDT 0.6B v3 on "Set ship nine oh six to done." (host check).
    const tokens = [' S', 'et', ' s', 'hi', 'p', ' ', '9', '0', '6', ' to', ' d', 'one', '.'];
    const timestamps = [0.24, 0.48, 0.64, 0.8, 0.96, 1.12, 1.2, 1.44, 1.68, 2.0, 2.16, 2.4, 2.64];

    expect(toWords(tokens, timestamps)).toEqual([
      { w: 'Set', startMs: 240, endMs: 640 },
      { w: 'ship', startMs: 640, endMs: 1120 },
      { w: '906', startMs: 1120, endMs: 2000 },
      { w: 'to', startMs: 2000, endMs: 2160 },
      { w: 'done.', startMs: 2160, endMs: 2640 + LAST_TOKEN_MS },
    ]);
  });

  it('reads the ▁ word marker the same as a space', () => {
    expect(toWords(['▁Pri', 'ya', '▁hi'], [0.1, 0.3, 0.5]).map((w) => w.w)).toEqual([
      'Priya',
      'hi',
    ]);
  });

  it('gives no words for no tokens, and refuses tokens and timestamps of different lengths', () => {
    expect(toWords([], [])).toEqual([]);
    expect(() => toWords([' a'], [])).toThrow();
  });
});
