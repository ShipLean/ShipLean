// What Settings › Voice shows: the status card, the Account row's value, the storage per model, and what the
// change-model sheet says. Pure: names, sizes and licences are the manifest's, so nothing here restates them.
import type { CommandModelId, MicPermission, ModelChoice, ModelStatus } from '../voice/contracts';
import { MODEL_FILES, type ModelFile, type ModelId } from '../voice/manifest';
import { chosenModel, formatGb, type ModelChoiceView } from './modelChoice';

export type MicState = Awaited<ReturnType<MicPermission['status']>>;

export interface Phone {
  // The phone's own name, e.g. "Galaxy S24 Ultra".
  model: string;
  // Memory as sold (D12).
  memoryGb: number;
}

export type VoiceStatusKind = 'ready' | 'downloading' | 'off' | 'micOff' | 'failed';

export interface VoiceStatusView {
  kind: VoiceStatusKind;
  line: string;
  // Off and failed offer Set up, which opens the model screen.
  setUp: boolean;
  // Whole number, only while downloading.
  percent: number | null;
}

const WORKING = new Set<ModelStatus['phase']>(['downloading', 'paused', 'checking']);

function modelOf(files: readonly ModelFile[], id: string): ModelId | null {
  return files.find((f) => f.id === id)?.model ?? null;
}

function sumBytes(files: readonly ModelFile[], model: ModelId): number {
  return files.filter((f) => f.model === model).reduce((sum, f) => sum + f.bytes, 0);
}

function nameOf(files: readonly ModelFile[], model: ModelId): string {
  const file = files.find((f) => f.model === model);
  if (file === undefined) throw new Error(`The manifest has no ${model}.`);
  return file.name;
}

const percentOf = (status: ModelStatus): number =>
  status.bytesTotal <= 0
    ? 0
    : Math.max(0, Math.min(100, Math.floor((status.bytesDone / status.bytesTotal) * 100)));

export const micIsOn = (mic: MicState): boolean => mic === 'granted';

export function voiceStatusView(
  status: ModelStatus,
  mic: MicState | null,
  phone: Phone,
): VoiceStatusView {
  if (WORKING.has(status.phase)) {
    const percent = percentOf(status);
    return {
      kind: 'downloading',
      line: `Downloading voice models · ${percent}%`,
      setUp: false,
      percent,
    };
  }
  if (status.phase === 'failed') {
    return { kind: 'failed', line: "Voice isn't ready", setUp: true, percent: null };
  }
  if (status.phase !== 'ready') {
    return { kind: 'off', line: 'Voice is off', setUp: true, percent: null };
  }
  if (mic !== null && !micIsOn(mic)) {
    return {
      kind: 'micOff',
      line: 'Voice is ready, but the microphone is off',
      setUp: false,
      percent: null,
    };
  }
  return {
    kind: 'ready',
    line: `Voice is ready · ${phone.model} · ${phone.memoryGb} GB memory`,
    setUp: false,
    percent: null,
  };
}

// The Voice row on the Account sheet: "Ready · Qwen3 4B", "Ready · mic off", "Downloading · 65%", "Off · Set up".
export function accountVoiceValue(
  status: ModelStatus,
  mic: MicState | null,
  files: readonly ModelFile[] = MODEL_FILES,
): string {
  if (WORKING.has(status.phase)) return `Downloading · ${percentOf(status)}%`;
  if (status.phase !== 'ready') return 'Off · Set up';
  if (mic !== null && !micIsOn(mic)) return 'Ready · mic off';
  return status.resolved === null ? 'Ready' : `Ready · ${nameOf(files, status.resolved)}`;
}

export interface ChangeUnderWay {
  name: string;
  percent: number;
  // Why it waits: wifi, space, network or checksum.
  reason: ModelStatus['reason'];
}

// A model change keeps the phase ready on the old model while the new one downloads and is checked.
export function changeUnderWay(
  status: ModelStatus,
  files: readonly ModelFile[] = MODEL_FILES,
): ChangeUnderWay | null {
  if (status.phase !== 'ready') return null;
  const pending = status.files.find((f) => f.state !== 'checked');
  if (pending === undefined) return null;
  const model = modelOf(files, pending.id);
  if (model === null) return null;
  return { name: nameOf(files, model), percent: percentOf(status), reason: status.reason };
}

// The command model row: the choice and the model in use, or the choice alone while voice is off.
export function commandModelLine(
  status: ModelStatus,
  view: ModelChoiceView,
  files: readonly ModelFile[] = MODEL_FILES,
): string {
  const model =
    status.phase === 'ready' && status.resolved !== null
      ? status.resolved
      : chosenModel(view, status.choice);
  const name = model === null ? 'No command model' : nameOf(files, model);
  return status.choice === 'auto' ? `Auto · ${name}` : name;
}

export interface StorageRow {
  model: ModelId;
  name: string;
  bytes: number;
}

export interface StorageView {
  rows: StorageRow[];
  totalBytes: number;
  // "Frees 3.1 GB. Voice turns off; your choices are kept."
  deleteLine: string;
}

// The bytes on the phone for each model, as the store counts them, the speech model first.
export function storageView(
  status: ModelStatus,
  files: readonly ModelFile[] = MODEL_FILES,
): StorageView {
  const byModel = new Map<ModelId, number>();
  for (const f of status.files) {
    const model = modelOf(files, f.id);
    if (model !== null) byModel.set(model, (byModel.get(model) ?? 0) + f.bytes);
  }
  // A change downloads into targetFiles(), so the model in use is missing from files: it is checked by definition.
  const inUse = status.phase === 'ready' ? status.resolved : null;
  if (inUse !== null && !byModel.has(inUse)) byModel.set(inUse, sumBytes(files, inUse));
  const rows = [...byModel]
    .filter(([, bytes]) => bytes > 0)
    .map(([model, bytes]): StorageRow => ({ model, name: nameOf(files, model), bytes }))
    .sort((a, b) => Number(b.model === 'parakeet') - Number(a.model === 'parakeet'));
  const totalBytes = rows.reduce((sum, r) => sum + r.bytes, 0);
  return {
    rows,
    totalBytes,
    deleteLine: `Frees ${formatGb(totalBytes)}. Voice turns off; your choices are kept.`,
  };
}

export interface ChangeSummary {
  // The model the choice resolves to, or null when it can't be picked here.
  target: CommandModelId | null;
  // Already in use: nothing to download.
  current: boolean;
  // The sentences under the choices; empty when there is nothing to say.
  lines: string[];
  downloadLabel: string | null;
}

// What picking `choice` does: what downloads, what voice keeps using until it is checked, what is then freed.
export function changeSummary(
  status: ModelStatus,
  view: ModelChoiceView,
  choice: ModelChoice,
  files: readonly ModelFile[] = MODEL_FILES,
): ChangeSummary {
  const target = chosenModel(view, choice);
  if (target === null) return { target: null, current: false, lines: [], downloadLabel: null };
  const inUse = status.phase === 'ready' ? status.resolved : null;
  if (target === inUse) {
    return {
      target,
      current: true,
      lines: [`${nameOf(files, target)} is the model in use.`],
      downloadLabel: null,
    };
  }
  const name = nameOf(files, target);
  const size = formatGb(sumBytes(files, target));
  const lines = [`${name} downloads ${size}${status.wifiOnly ? ' on Wi-Fi' : ''}.`];
  if (inUse !== null) {
    const old = nameOf(files, inUse);
    lines.push(
      `Voice keeps using ${old} until ${name} is checked.`,
      `Then ${old} is deleted to free ${formatGb(sumBytes(files, inUse))}.`,
    );
  }
  return { target, current: false, lines, downloadLabel: `Download ${size}` };
}

export interface LicenceEntry {
  model: ModelId;
  name: string;
  author: string;
  licence: string;
  link: string;
  changes: string;
}

// "Apache-2.0" reads "Apache 2.0"; the rest are shown as the manifest has them.
export const licenceLabel = (licence: string): string =>
  licence.startsWith('Apache-') ? licence.replace('-', ' ') : licence;

// One entry per model: a model split over several files is one entry, from its first file.
export function licenceEntries(files: readonly ModelFile[] = MODEL_FILES): LicenceEntry[] {
  const seen = new Set<ModelId>();
  const out: LicenceEntry[] = [];
  for (const f of files) {
    if (seen.has(f.model)) continue;
    seen.add(f.model);
    out.push({
      model: f.model,
      name: f.name,
      author: f.author,
      licence: f.licence,
      link: f.link,
      changes: f.changes,
    });
  }
  return out;
}

function sharedWords(names: string[]): string {
  const split = names.map((n) => n.split(' '));
  const shared: string[] = [];
  for (const [i, word] of split[0].entries()) {
    if (!split.every((words) => words[i] === word)) break;
    shared.push(word);
  }
  return shared.join(' ');
}

// The Model licences row: "Parakeet TDT 0.6B (CC-BY-4.0) · Qwen3 (Apache 2.0)". Models of one author are one
// name: the words their names share.
export function licencesLine(files: readonly ModelFile[] = MODEL_FILES): string {
  const byAuthor = new Map<string, LicenceEntry[]>();
  for (const e of licenceEntries(files))
    byAuthor.set(e.author, [...(byAuthor.get(e.author) ?? []), e]);
  return [...byAuthor.values()]
    .map((group) => {
      const name = group.length === 1 ? group[0].name : sharedWords(group.map((e) => e.name));
      const licences = [...new Set(group.map((e) => licenceLabel(e.licence)))].join(', ');
      return `${name} (${licences})`;
    })
    .join(' · ');
}
