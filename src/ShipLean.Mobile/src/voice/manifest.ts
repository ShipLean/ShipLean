// The pinned model files and Auto's rule for which command model a phone gets. Pinned Hugging Face URLs with SHA-256,
// no CDN; self-hosted servers use the same URLs. Nothing here makes a request.
export type ModelId = 'parakeet' | 'qwen3-1.7b' | 'qwen3-4b';

export interface ModelFile {
  id: string;
  model: ModelId;
  // For the licences page.
  name: string;
  author: string;
  link: string;
  changes: string;
  // The pinned download.
  url: string;
  bytes: number;
  sha256: string;
  licence: 'CC-BY-4.0' | 'Apache-2.0';
}

export const MODEL_FILES: ModelFile[] = [
  {
    id: 'parakeet-tdt-0.6b-int8',
    model: 'parakeet',
    name: 'Parakeet TDT 0.6B',
    author: 'NVIDIA',
    // TODO: the model card of the pinned Parakeet version
    link: '',
    changes: 'Converted to int8 ONNX for sherpa-onnx.',
    // TODO: the pinned Hugging Face URL of the sherpa-onnx int8 package
    url: '',
    // TODO: the pinned file's size in bytes
    bytes: 0,
    // TODO: the pinned file's SHA-256
    sha256: '',
    licence: 'CC-BY-4.0',
  },
  {
    id: 'qwen3-1.7b-q4_k_m',
    model: 'qwen3-1.7b',
    name: 'Qwen3 1.7B',
    author: 'Qwen team, Alibaba Cloud',
    // TODO: the model card of the pinned Qwen3 1.7B
    link: '',
    changes: 'Quantised to Q4_K_M GGUF.',
    // TODO: the pinned Hugging Face URL of the Q4_K_M GGUF
    url: '',
    // TODO: the pinned file's size in bytes
    bytes: 0,
    // TODO: the pinned file's SHA-256
    sha256: '',
    licence: 'Apache-2.0',
  },
  {
    id: 'qwen3-4b-q4_k_m',
    model: 'qwen3-4b',
    name: 'Qwen3 4B',
    author: 'Qwen team, Alibaba Cloud',
    // TODO: the model card of the pinned Qwen3 4B
    link: '',
    changes: 'Quantised to Q4_K_M GGUF.',
    // TODO: the pinned Hugging Face URL of the Q4_K_M GGUF
    url: '',
    // TODO: the pinned file's size in bytes
    bytes: 0,
    // TODO: the pinned file's SHA-256
    sha256: '',
    licence: 'Apache-2.0',
  },
];

const SOLD_GB = [4, 6, 8, 12, 16, 24];

// Phones report a little less memory than they are sold with (a 12 GB phone reports about 11.2): the size sold.
export function soldGb(reported: number): number {
  return SOLD_GB.find((gb) => gb >= reported) ?? Math.ceil(reported);
}

export interface ModelAdvice {
  auto: 'qwen3-4b' | 'qwen3-1.7b' | null;
  fourB: 'ok' | 'warn' | 'blocked';
  reason: 'lowMemory' | null;
}

// Auto: 11 GB reported or more gets 4B; the 8 GB class gets 1.7B and may pick 4B with a warning; 6 GB gets 1.7B
// only; less gets no voice.
export function chooseModel(reportedGb: number): ModelAdvice {
  if (reportedGb >= 11) return { auto: 'qwen3-4b', fourB: 'ok', reason: null };
  const sold = soldGb(reportedGb);
  if (sold >= 8) return { auto: 'qwen3-1.7b', fourB: 'warn', reason: null };
  if (sold >= 6) return { auto: 'qwen3-1.7b', fourB: 'blocked', reason: null };
  return { auto: null, fourB: 'blocked', reason: 'lowMemory' };
}
