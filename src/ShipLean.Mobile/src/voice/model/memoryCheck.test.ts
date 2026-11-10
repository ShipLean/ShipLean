import { describe, expect, it } from 'vitest';
import { availableBytes, bytesNeeded, HEADROOM_BYTES, memoryCheck, N_CTX } from './memoryCheck';

const GB = 1024 ** 3;
// The Q4_K_M GGUF sizes D2 names: ~2.5 GB for 4B, ~1.1 GB for 1.7B.
const FOUR_B = 2.5 * GB;
const ONE_POINT_SEVEN_B = 1.1 * GB;

describe('memoryCheck', () => {
  it('4B does not fit in 3 GB available; 1.7B does', () => {
    const three = 3 * GB;

    const fourB = memoryCheck('qwen3-4b', FOUR_B, three);
    const small = memoryCheck('qwen3-1.7b', ONE_POINT_SEVEN_B, three);

    expect(fourB.fits).toBe(false);
    expect(fourB.needed).toBeGreaterThan(three);
    expect(small.fits).toBe(true);
    expect(small.needed).toBeLessThanOrEqual(three);
  });

  it('counts the file, the KV cache for n_ctx and the headroom', () => {
    // 1.7B: 28 layers × 8 KV heads × 128 × K and V × 2 bytes per token.
    expect(bytesNeeded('qwen3-1.7b', 1000, N_CTX)).toBe(1000 + 114_688 * N_CTX + HEADROOM_BYTES);
    // 4B: 36 layers.
    expect(bytesNeeded('qwen3-4b', 1000, 100)).toBe(1000 + 147_456 * 100 + HEADROOM_BYTES);
  });

  it('gives 4B to a 12 GB phone and only 1.7B to the 8 GB class, by the memory each reports', () => {
    // A 12 GB phone reports about 11.2 GB; an 8 GB one about 7.4 (D2 notes).
    expect(memoryCheck('qwen3-4b', FOUR_B, availableBytes(11.2)).fits).toBe(true);
    expect(memoryCheck('qwen3-4b', FOUR_B, availableBytes(7.4)).fits).toBe(false);
    expect(memoryCheck('qwen3-1.7b', ONE_POINT_SEVEN_B, availableBytes(7.4)).fits).toBe(true);
    expect(memoryCheck('qwen3-1.7b', ONE_POINT_SEVEN_B, availableBytes(0)).fits).toBe(false);
  });
});
