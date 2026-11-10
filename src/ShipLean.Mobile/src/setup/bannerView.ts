// What the voice banner on list and detail screens shows for a model store status. Pure: sizes and names are the
// manifest's, and the time left is passed in (it comes from the rate seen while the app is open).
import type { CommandModelId, ModelStatus } from '../voice/contracts';
import { MODEL_FILES, type ModelFile } from '../voice/manifest';
import { amountOf } from './downloadView';

export type BannerView =
  // Downloading or checking: progress, and a tap opens the Downloading page.
  | { kind: 'gettingReady'; text: string; fraction: number }
  // Paused: the same bar, stopped, and the reason.
  | { kind: 'paused'; text: string; fraction: number }
  // Skipped, or deleted: Set up opens the model screen.
  | { kind: 'off'; text: string; action: string }
  // The command model didn't fit in memory: the sheet offers the smaller one.
  | { kind: 'memory'; text: string; action: string };

export interface BannerOptions {
  // Setup has been offered on this phone, so a status of none means voice was turned off, not never set up.
  setupOffered: boolean;
  // "about 3 min", or null until a rate is known.
  left: string | null;
}

const PAUSE_REASON: Record<NonNullable<ModelStatus['reason']>, string> = {
  wifi: ' · waiting for Wi-Fi',
  network: ' · waiting for a connection',
  space: '',
  checksum: '',
  memory: '',
};

function fractionOf(status: ModelStatus): number {
  return status.bytesTotal <= 0 ? 0 : Math.min(1, status.bytesDone / status.bytesTotal);
}

// The banner for a status, or null when there is nothing to say: voice ready (a model change in progress keeps the
// phase ready, and Settings shows it), never offered setup, or a download that failed for another reason.
export function bannerView(status: ModelStatus, opts: BannerOptions): BannerView | null {
  switch (status.phase) {
    case 'downloading':
    case 'checking': {
      const parts = [
        status.bytesTotal > 0
          ? `${amountOf(status.bytesDone, status.bytesTotal)}${status.wifiOnly ? ' on Wi-Fi' : ''}`
          : null,
        opts.left,
      ].filter((p): p is string => p !== null);
      const detail = parts.length === 0 ? '' : ` · ${parts.join(' · ')}`;
      return {
        kind: 'gettingReady',
        text: `Voice is getting ready${detail}. Read, tap and type in the meantime.`,
        fraction: fractionOf(status),
      };
    }
    case 'paused':
      return {
        kind: 'paused',
        text: `Voice download paused${status.reason === null ? '' : PAUSE_REASON[status.reason]}`,
        fraction: fractionOf(status),
      };
    case 'skipped':
      return { kind: 'off', text: 'Voice is off', action: 'Set up' };
    case 'none':
      return opts.setupOffered ? { kind: 'off', text: 'Voice is off', action: 'Set up' } : null;
    case 'failed':
      return status.reason === 'memory'
        ? { kind: 'memory', text: 'Voice is off · not enough memory', action: 'See options' }
        : null;
    case 'ready':
      return null;
  }
}

function modelOf(files: readonly ModelFile[], model: CommandModelId) {
  const mine = files.filter((f) => f.model === model);
  if (mine.length === 0) throw new Error(`The manifest has no ${model}.`);
  return { name: mine[0].name, bytes: mine.reduce((sum, f) => sum + f.bytes, 0) };
}

export interface MemoryView {
  // The model that didn't fit.
  failed: string;
  // The smaller model to offer, or null when the one that failed is already the smaller one.
  smaller: { model: CommandModelId; name: string; bytes: number } | null;
}

export function memoryView(
  status: ModelStatus,
  files: readonly ModelFile[] = MODEL_FILES,
): MemoryView {
  const failed =
    status.resolved === null ? 'The command model' : modelOf(files, status.resolved).name;
  if (status.resolved !== 'qwen3-4b') return { failed, smaller: null };
  return { failed, smaller: { model: 'qwen3-1.7b', ...modelOf(files, 'qwen3-1.7b') } };
}
