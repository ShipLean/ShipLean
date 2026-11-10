// The pinned model files and Auto's rule for which command model a phone gets. Pinned Hugging Face URLs with SHA-256,
// no CDN; self-hosted servers use the same URLs. Nothing here makes a request.
export type ModelId = 'parakeet' | 'qwen3-1.7b' | 'qwen3-4b';

export interface ModelFile {
  id: string;
  model: ModelId;
  // The file's own name in its Hugging Face repo. sherpa-onnx finds a model's files by name in one directory, so a
  // file is stored as <models root>/<model>/<fileName>.
  fileName: string;
  // For the licences page.
  name: string;
  author: string;
  link: string;
  changes: string;
  // The pinned download: a Hugging Face URL at a fixed revision, never a branch.
  url: string;
  bytes: number;
  sha256: string;
  licence: 'CC-BY-4.0' | 'Apache-2.0';
}

// Parakeet is four files of one sherpa-onnx package (the bpe.vocab its hotwords need is made on the phone from
// tokens.txt). Pinned: csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8 @ ed5d7456.
const PARAKEET = {
  name: 'Parakeet TDT 0.6B',
  author: 'NVIDIA',
  link: 'https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3',
  changes: 'Converted to int8 ONNX for sherpa-onnx by the k2-fsa project.',
  licence: 'CC-BY-4.0',
} as const;

export const MODEL_FILES: ModelFile[] = [
  {
    id: 'parakeet-encoder',
    model: 'parakeet',
    fileName: 'encoder.int8.onnx',
    ...PARAKEET,
    url: 'https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/ed5d745623472f97cfc2e6c2916ee248798fc5a8/encoder.int8.onnx',
    bytes: 652_282_294,
    sha256: '1afbe69c1ffdabee94755f42e4daf1d93549131a3da11356bda84fcef0f2c479',
  },
  {
    id: 'parakeet-decoder',
    model: 'parakeet',
    fileName: 'decoder.int8.onnx',
    ...PARAKEET,
    url: 'https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/ed5d745623472f97cfc2e6c2916ee248798fc5a8/decoder.int8.onnx',
    bytes: 11_845_274,
    sha256: '0222a86147299d4dee90edf95b6d13827edb42ad8b7c9cf0b2582f030937c0b5',
  },
  {
    id: 'parakeet-joiner',
    model: 'parakeet',
    fileName: 'joiner.int8.onnx',
    ...PARAKEET,
    url: 'https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/ed5d745623472f97cfc2e6c2916ee248798fc5a8/joiner.int8.onnx',
    bytes: 6_355_277,
    sha256: 'cbd02f3696e9944970111fb439d9c3fd365a80374565f13f2b9c7007d253fc98',
  },
  {
    id: 'parakeet-tokens',
    model: 'parakeet',
    fileName: 'tokens.txt',
    ...PARAKEET,
    url: 'https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/ed5d745623472f97cfc2e6c2916ee248798fc5a8/tokens.txt',
    bytes: 93_939,
    sha256: 'd58544679ea4bc6ac563d1f545eb7d474bd6cfa467f0a6e2c1dc1c7d37e3c35d',
  },
  {
    // Qwen publishes only Q8_0 of the 1.7B model; Q4_K_M is Unsloth's. Pinned: unsloth/Qwen3-1.7B-GGUF @ d7f544ee.
    id: 'qwen3-1.7b-q4_k_m',
    model: 'qwen3-1.7b',
    fileName: 'Qwen3-1.7B-Q4_K_M.gguf',
    name: 'Qwen3 1.7B',
    author: 'Qwen team, Alibaba Cloud',
    link: 'https://huggingface.co/Qwen/Qwen3-1.7B',
    changes: 'Quantised to Q4_K_M GGUF by Unsloth.',
    url: 'https://huggingface.co/unsloth/Qwen3-1.7B-GGUF/resolve/d7f544eead698dbd1f15126ef60b45a1e1933222/Qwen3-1.7B-Q4_K_M.gguf',
    bytes: 1_107_409_472,
    sha256: 'b139949c5bd74937ad8ed8c8cf3d9ffb1e99c866c823204dc42c0d91fa181897',
    licence: 'Apache-2.0',
  },
  {
    // Pinned: Qwen/Qwen3-4B-GGUF @ bc640142.
    id: 'qwen3-4b-q4_k_m',
    model: 'qwen3-4b',
    fileName: 'Qwen3-4B-Q4_K_M.gguf',
    name: 'Qwen3 4B',
    author: 'Qwen team, Alibaba Cloud',
    link: 'https://huggingface.co/Qwen/Qwen3-4B',
    changes: 'Quantised to Q4_K_M GGUF by the Qwen team.',
    url: 'https://huggingface.co/Qwen/Qwen3-4B-GGUF/resolve/bc640142c66e1fdd12af0bd68f40445458f3869b/Qwen3-4B-Q4_K_M.gguf',
    bytes: 2_497_280_256,
    sha256: '7485fe6f11af29433bc51cab58009521f205840f5b4ae3a32fa7f92e8534fdf5',
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
