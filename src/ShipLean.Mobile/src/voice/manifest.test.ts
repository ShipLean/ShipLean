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

  it('lists every file with its model and licence', () => {
    expect(MODEL_FILES.map((f) => [f.id, f.model, f.fileName, f.licence])).toEqual([
      ['parakeet-encoder', 'parakeet', 'encoder.int8.onnx', 'CC-BY-4.0'],
      ['parakeet-decoder', 'parakeet', 'decoder.int8.onnx', 'CC-BY-4.0'],
      ['parakeet-joiner', 'parakeet', 'joiner.int8.onnx', 'CC-BY-4.0'],
      ['parakeet-tokens', 'parakeet', 'tokens.txt', 'CC-BY-4.0'],
      ['qwen3-1.7b-q4_k_m', 'qwen3-1.7b', 'Qwen3-1.7B-Q4_K_M.gguf', 'Apache-2.0'],
      ['qwen3-4b-q4_k_m', 'qwen3-4b', 'Qwen3-4B-Q4_K_M.gguf', 'Apache-2.0'],
    ]);
  });

  it('pins every file to a Hugging Face revision with its size and SHA-256', () => {
    for (const f of MODEL_FILES) {
      expect(f.url).toMatch(/^https:\/\/huggingface\.co\/[^/]+\/[^/]+\/resolve\/[0-9a-f]{40}\//);
      expect(f.url.endsWith(`/${f.fileName}`)).toBe(true);
      expect(f.sha256).toMatch(/^[0-9a-f]{64}$/);
      expect(f.bytes).toBeGreaterThan(0);
      expect(f.link).toMatch(/^https:\/\/huggingface\.co\//);
    }
  });
});
