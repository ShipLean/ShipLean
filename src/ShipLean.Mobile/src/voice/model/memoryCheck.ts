// Whether a command model fits in the memory the phone gives the app, before it is loaded (D2 notes, P-S3). Pure: the
// host eval and vitest load it in node.
import type { CommandModelId } from '../contracts';

// The context the adapter loads with: the screen's prefix (under ~1.5k tokens), the context, the command and the
// calls fit well inside it.
export const N_CTX = 4096;

// The KV cache per token at f16: 2 (K and V) × layers × KV heads × head size × 2 bytes. Qwen3 1.7B has 28 layers and
// Qwen3 4B 36, both with 8 KV heads of 128 (their config.json).
const KV_BYTES_PER_TOKEN: Record<CommandModelId, number> = {
  'qwen3-1.7b': 2 * 28 * 8 * 128 * 2,
  'qwen3-4b': 2 * 36 * 8 * 128 * 2,
};

// llama.cpp's compute buffers, the grammar and the runtime around the weights and the cache.
export const HEADROOM_BYTES = 512 * 1024 ** 2;

// The share of the memory a phone reports that a foreground app can count on. Android's low-memory killer and iOS's
// per-app limit (with the increased-memory-limit entitlement) leave an app well under the phone's RAM, and Parakeet
// (~0.7 GB) and the app itself need the rest. On a 12 GB phone that leaves ~4.5 GB (4B fits); on the 8 GB class
// ~3 GB (4B doesn't, 1.7B does).
// TODO: calibrate against the device runs (S22, S24, iPhone), and read os_proc_available_memory() on iOS once a
// native module exposes it.
export const APP_SHARE = 0.4;

// The bytes a model needs loaded: its file, the KV cache for nCtx tokens and the headroom.
export function bytesNeeded(
  model: CommandModelId,
  fileBytes: number,
  nCtx: number = N_CTX,
): number {
  return fileBytes + KV_BYTES_PER_TOKEN[model] * nCtx + HEADROOM_BYTES;
}

// What the app may use, from the memory the phone reports (ModelStore.deviceMemory().reportedGb).
export function availableBytes(reportedGb: number): number {
  return Math.floor(reportedGb * 1024 ** 3 * APP_SHARE);
}

export interface MemoryCheck {
  fits: boolean;
  needed: number;
  available: number;
}

export function memoryCheck(
  model: CommandModelId,
  fileBytes: number,
  available: number,
  nCtx: number = N_CTX,
): MemoryCheck {
  const needed = bytesNeeded(model, fileBytes, nCtx);
  return { fits: needed <= available, needed, available };
}
