import { describe, expect, it } from 'vitest';
import { MAX_PROMPT_BYTES, promptByteSize } from './byteSize';

describe('promptByteSize', () => {
  it('counts ASCII as one byte each', () => {
    expect(promptByteSize('abc')).toBe(3);
  });

  it('counts multi-byte characters as their UTF-8 bytes', () => {
    // é is 2 bytes, € is 3, an emoji outside the BMP is 4.
    expect(promptByteSize('é€😀')).toBe(9);
  });

  it('counts a CRLF line break as one byte', () => {
    expect(promptByteSize('a\r\nb')).toBe(3);
  });

  it('keeps a lone CR', () => {
    expect(promptByteSize('a\rb')).toBe(3);
  });

  it('is zero for the empty text', () => {
    expect(promptByteSize('')).toBe(0);
  });

  it('measures against 64 KB', () => {
    expect(MAX_PROMPT_BYTES).toBe(65536);
    expect(promptByteSize('€'.repeat(21846))).toBeGreaterThan(MAX_PROMPT_BYTES);
  });
});
