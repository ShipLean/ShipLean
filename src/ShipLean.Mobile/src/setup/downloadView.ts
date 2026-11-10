// What the Downloading page shows for a model store status: overall progress, a row per model, the time left from
// the rate seen so far, and the no-space figures. Pure: sizes and names are the manifest's.
import type { CommandModelId, ModelStatus } from '../voice/contracts';
import { MODEL_FILES, type ModelFile, type ModelId } from '../voice/manifest';
import { formatGb } from './modelChoice';

export interface FileRowView {
  model: ModelId;
  name: string;
  role: 'Speech to text' | 'Command model';
  bytes: number;
  doneBytes: number;
  // 0..1
  fraction: number;
  // stopped: the download ran out of space on this one.
  state: 'checked' | 'downloading' | 'waiting' | 'failed' | 'stopped';
  detail: string;
}

export interface DownloadView {
  // Whole number, 0..100.
  percent: number;
  doneBytes: number;
  totalBytes: number;
  // "1.2 of 3.1 GB"
  amount: string;
  rows: FileRowView[];
}

const ROLE: Record<ModelId, FileRowView['role']> = {
  parakeet: 'Speech to text',
  'qwen3-1.7b': 'Command model',
  'qwen3-4b': 'Command model',
};

// The "0.6 of 2.5 GB" style.
export function amountOf(done: number, total: number): string {
  return `${(done / 1e9).toFixed(1)} of ${formatGb(total)}`;
}

function modelOf(files: readonly ModelFile[], id: string): ModelId | null {
  return files.find((f) => f.id === id)?.model ?? null;
}

function modelBytes(files: readonly ModelFile[], model: ModelId): number {
  return files.filter((f) => f.model === model).reduce((sum, f) => sum + f.bytes, 0);
}

export function downloadView(
  status: ModelStatus,
  files: readonly ModelFile[] = MODEL_FILES,
): DownloadView {
  const models: ModelId[] = [];
  for (const f of status.files) {
    const model = modelOf(files, f.id);
    if (model !== null && !models.includes(model)) models.push(model);
  }
  // The speech model first, as the design lists it.
  models.sort((a, b) => Number(b === 'parakeet') - Number(a === 'parakeet'));
  const stopped = status.reason === 'space';
  const rows = models.map((model): FileRowView => {
    const mine = status.files.filter((f) => modelOf(files, f.id) === model);
    const bytes = modelBytes(files, model);
    const doneBytes = Math.min(
      bytes,
      mine.reduce((sum, f) => sum + f.bytes, 0),
    );
    const role = ROLE[model];
    const name = files.find((f) => f.model === model)?.name ?? model;
    const fraction = bytes === 0 ? 0 : doneBytes / bytes;
    let state: FileRowView['state'];
    let detail: string;
    if (mine.every((f) => f.state === 'checked')) {
      state = 'checked';
      detail = `${role} · downloaded and checked`;
    } else if (stopped) {
      state = 'stopped';
      detail = `${role} · Stopped at ${amountOf(doneBytes, bytes)} · needs ${formatGb(bytes - doneBytes)} more space`;
    } else if (mine.some((f) => f.state === 'failed')) {
      state = 'failed';
      detail = `${role} · didn't finish`;
    } else if (mine.some((f) => f.state === 'downloading') || doneBytes > 0) {
      state = 'downloading';
      detail = `${role} · ${amountOf(doneBytes, bytes)}`;
    } else {
      state = 'waiting';
      detail = `${role} · waiting`;
    }
    return { model, name, role, bytes, doneBytes, fraction, state, detail };
  });
  const percent =
    status.phase === 'ready'
      ? 100
      : status.bytesTotal <= 0
        ? 0
        : Math.max(0, Math.min(100, Math.floor((status.bytesDone / status.bytesTotal) * 100)));
  return {
    percent,
    doneBytes: status.bytesDone,
    totalBytes: status.bytesTotal,
    amount: amountOf(status.bytesDone, status.bytesTotal),
    rows,
  };
}

export interface Sample {
  at: number;
  bytes: number;
}

const RATE_WINDOW_MS = 30_000;
const MIN_SPAN_MS = 2_000;

// Keeps the samples of the last half minute, so the rate follows the connection.
export function withSample(samples: readonly Sample[], next: Sample): Sample[] {
  return [...samples, next].filter((s) => next.at - s.at <= RATE_WINDOW_MS);
}

// Bytes a second over the samples, or null until two of them are far enough apart to say.
export function rateOf(samples: readonly Sample[]): number | null {
  if (samples.length < 2) return null;
  const first = samples[0];
  const last = samples[samples.length - 1];
  if (last.at - first.at < MIN_SPAN_MS) return null;
  const rate = ((last.bytes - first.bytes) / (last.at - first.at)) * 1000;
  return rate > 0 ? rate : null;
}

// "about 6 min", from the rate seen so far. An estimate: null when there is no rate yet.
export function timeLeft(remainingBytes: number, rate: number | null): string | null {
  if (rate === null || remainingBytes <= 0) return null;
  const minutes = Math.round(remainingBytes / rate / 60);
  if (minutes < 1) return 'less than a minute';
  if (minutes < 60) return `about ${minutes} min`;
  return `about ${Math.floor(minutes / 60)} h ${minutes % 60} min`;
}

export interface NoSpaceView {
  // The model that stopped, and what is left to fetch for the download as a whole.
  model: string;
  neededBytes: number;
  freeBytes: number;
  // Switching to the smaller model, when its download fits once the stopped one's partial files are removed.
  smaller: { model: CommandModelId; name: string; bytes: number } | null;
}

// The no-space figures, or null when the download didn't stop for space.
export function noSpaceView(
  status: ModelStatus,
  files: readonly ModelFile[] = MODEL_FILES,
): NoSpaceView | null {
  if (status.reason !== 'space') return null;
  const stopped = status.resolved;
  const view = downloadView(status, files);
  const row = view.rows.find((r) => r.state === 'stopped' && r.model !== 'parakeet');
  const model = row?.name ?? files.find((f) => f.model === stopped)?.name ?? 'The model';
  let smaller: NoSpaceView['smaller'] = null;
  if (stopped === 'qwen3-4b') {
    const small = files.filter((f) => f.model === 'qwen3-1.7b');
    const bytes = small.reduce((sum, f) => sum + f.bytes, 0);
    const have = (ids: Set<string>) =>
      status.files.filter((f) => ids.has(f.id)).reduce((sum, f) => sum + f.bytes, 0);
    const smallHave = have(new Set(small.map((f) => f.id)));
    const stoppedHave = have(new Set(files.filter((f) => f.model === 'qwen3-4b').map((f) => f.id)));
    // Removing the stopped model's partial files frees their space.
    if (bytes - smallHave <= status.freeBytes + stoppedHave && small.length > 0) {
      smaller = { model: 'qwen3-1.7b', name: small[0].name, bytes };
    }
  }
  return {
    model,
    neededBytes: Math.max(0, status.bytesTotal - status.bytesDone),
    freeBytes: status.freeBytes,
    smaller,
  };
}
