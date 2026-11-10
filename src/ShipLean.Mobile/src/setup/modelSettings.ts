// The model store's saved state. It is per phone, not per account: the models outlive sign-out, so these keys sit
// outside any account scope and nothing clears them on sign-out.
import type { KeyValueStore } from '@shiplean/core/platform';
import type { CommandModelId, ModelChoice, ModelStatus } from '../voice/contracts';

export type FileState = ModelStatus['files'][number]['state'];

// The download that is under way: it survives a restart, so the store carries on where it was.
export interface SavedDownload {
  // When this download was asked for, from the injected clock (ms since the epoch).
  startedAt: number;
  // useMobileDataOnce() was called for this download. It ends with it.
  mobileDataOnce: boolean;
  // Pause was tapped: a restart doesn't resume it.
  paused: boolean;
}

export interface ModelSettings {
  choice: ModelChoice;
  // On by default (D7): ask before using mobile data.
  wifiOnly: boolean;
  skipped: boolean;
  // The command model voice runs on: set once every file of it is checked.
  inUse: CommandModelId | null;
  files: Record<string, FileState>;
  download: SavedDownload | null;
}

export const MODEL_SETTINGS_KEYS = {
  choice: 'shiplean.voice.choice',
  wifiOnly: 'shiplean.voice.wifiOnly',
  skipped: 'shiplean.voice.skipped',
  inUse: 'shiplean.voice.inUse',
  files: 'shiplean.voice.files',
  download: 'shiplean.voice.download',
} as const;

const CHOICES: readonly ModelChoice[] = ['auto', 'qwen3-1.7b', 'qwen3-4b'];
const COMMAND_MODELS: readonly CommandModelId[] = ['qwen3-1.7b', 'qwen3-4b'];
const FILE_STATES: readonly FileState[] = ['pending', 'downloading', 'checked', 'failed'];

export const DEFAULT_MODEL_SETTINGS: ModelSettings = {
  choice: 'auto',
  wifiOnly: true,
  skipped: false,
  inUse: null,
  files: {},
  download: null,
};

function parse(raw: string | null): unknown {
  if (raw === null) return null;
  try {
    return JSON.parse(raw) as unknown;
  } catch {
    return null;
  }
}

function oneOf<T extends string>(
  raw: string | null,
  values: readonly T[],
  fallback: T | null,
): T | null {
  return values.find((v) => v === raw) ?? fallback;
}

function loadFiles(raw: string | null): Record<string, FileState> {
  const value = parse(raw);
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return {};
  const files: Record<string, FileState> = {};
  for (const [id, state] of Object.entries(value)) {
    const known = FILE_STATES.find((s) => s === state);
    if (known !== undefined) files[id] = known;
  }
  return files;
}

function loadDownload(raw: string | null): SavedDownload | null {
  const value = parse(raw);
  if (value === null || typeof value !== 'object') return null;
  const { startedAt, mobileDataOnce, paused } = value as Partial<SavedDownload>;
  if (typeof startedAt !== 'number') return null;
  return { startedAt, mobileDataOnce: mobileDataOnce === true, paused: paused === true };
}

export function loadModelSettings(kv: KeyValueStore): ModelSettings {
  const keys = MODEL_SETTINGS_KEYS;
  return {
    choice: oneOf(kv.getItem(keys.choice), CHOICES, 'auto') ?? 'auto',
    wifiOnly: kv.getItem(keys.wifiOnly) !== 'false',
    skipped: kv.getItem(keys.skipped) === 'true',
    inUse: oneOf(kv.getItem(keys.inUse), COMMAND_MODELS, null),
    files: loadFiles(kv.getItem(keys.files)),
    download: loadDownload(kv.getItem(keys.download)),
  };
}

export function saveModelSettings(kv: KeyValueStore, s: ModelSettings): void {
  const keys = MODEL_SETTINGS_KEYS;
  kv.setItem(keys.choice, s.choice);
  kv.setItem(keys.wifiOnly, String(s.wifiOnly));
  kv.setItem(keys.skipped, String(s.skipped));
  if (s.inUse === null) kv.removeItem(keys.inUse);
  else kv.setItem(keys.inUse, s.inUse);
  kv.setItem(keys.files, JSON.stringify(s.files));
  if (s.download === null) kv.removeItem(keys.download);
  else kv.setItem(keys.download, JSON.stringify(s.download));
}
