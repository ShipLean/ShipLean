// What the model screen of setup shows for this phone: the rows, what Auto picked, and the download size. Pure: the
// thresholds are chooseModel's and the names and sizes are the manifest's, so nothing here restates them.
import type { CommandModelId, ModelChoice } from '../voice/contracts';
import { MODEL_FILES, chooseModel, soldGb, type ModelFile, type ModelId } from '../voice/manifest';

export type FourBAvailability = 'ok' | 'warn' | 'blocked';

export interface CommandRow {
  id: CommandModelId;
  name: string;
  bytes: number;
  tag: 'Quicker' | 'More accurate';
  availability: FourBAvailability;
  // What it is good at, with the estimated time per command on this phone.
  detail: string;
  // The 8 GB class picking 4B: slow, and may be closed when memory runs low.
  warning: string | null;
  // Why the row can't be picked.
  blockedReason: string | null;
}

export interface ModelChoiceView {
  // false when the phone has too little memory for any command model.
  voice: boolean;
  noVoiceReason: string | null;
  // The memory as sold, as D12 shows it.
  memoryGb: number;
  auto: { model: CommandModelId; name: string; line: string } | null;
  commandRows: CommandRow[];
  speech: { name: string; bytes: number };
  // D14: the times are estimates by phone class, not measurements.
  estimateNote: string;
}

export interface Memory {
  reportedGb: number;
  soldGb: number;
}

// The estimated seconds a command takes, by the phone class chooseModel puts a phone in (D14). A small table until
// the test set gives real numbers; null where the model can't be picked.
const ESTIMATES: Record<FourBAvailability, Record<CommandModelId, string | null>> = {
  ok: { 'qwen3-1.7b': '1 s', 'qwen3-4b': '2 s' },
  warn: { 'qwen3-1.7b': '5 s', 'qwen3-4b': '12 s' },
  blocked: { 'qwen3-1.7b': '3 s', 'qwen3-4b': null },
};

export const ESTIMATE_NOTE = 'Times are estimates for phones like this one.';

function sumBytes(files: readonly ModelFile[], model: ModelId): number {
  return files.filter((f) => f.model === model).reduce((sum, f) => sum + f.bytes, 0);
}

function nameOf(files: readonly ModelFile[], model: ModelId): string {
  const file = files.find((f) => f.model === model);
  if (file === undefined) throw new Error(`The manifest has no ${model}.`);
  return file.name;
}

// The memory, as sold, of the smallest phone for which the predicate holds on chooseModel's answer.
function smallestGb(holds: (gb: number) => boolean): number {
  for (let gb = 1; ; gb++) if (holds(gb)) return soldGb(gb);
}

export function modelChoiceView(
  memory: Memory,
  files: readonly ModelFile[] = MODEL_FILES,
): ModelChoiceView {
  const advice = chooseModel(memory.reportedGb);
  const speech = { name: nameOf(files, 'parakeet'), bytes: sumBytes(files, 'parakeet') };
  const estimateNote = ESTIMATE_NOTE;
  if (advice.auto === null) {
    const needs = smallestGb((gb) => chooseModel(gb).auto !== null);
    return {
      voice: false,
      noVoiceReason: `Voice needs ${needs} GB of memory or more. This phone has ${memory.soldGb} GB, so it stays off. Everything else in the app works.`,
      memoryGb: memory.soldGb,
      auto: null,
      commandRows: [],
      speech,
      estimateNote,
    };
  }
  const needs4B = smallestGb((gb) => chooseModel(gb).fourB !== 'blocked');
  const row = (
    id: CommandModelId,
    tag: CommandRow['tag'],
    availability: FourBAvailability,
    blurb: string,
  ): CommandRow => {
    const estimate = ESTIMATES[advice.fourB][id];
    return {
      id,
      name: nameOf(files, id),
      bytes: sumBytes(files, id),
      tag,
      availability,
      detail: estimate === null ? blurb : `About ${estimate} a command here. ${blurb}`,
      warning:
        availability === 'warn'
          ? `Not recommended with ${memory.soldGb} GB: slow, and may be closed when memory runs low.`
          : null,
      blockedReason:
        availability === 'blocked'
          ? `Needs ${needs4B} GB of memory. This phone has ${memory.soldGb} GB.`
          : null,
    };
  };
  return {
    voice: true,
    noVoiceReason: null,
    memoryGb: memory.soldGb,
    auto: {
      model: advice.auto,
      name: nameOf(files, advice.auto),
      line: `This phone: ${memory.soldGb} GB → ${nameOf(files, advice.auto)}`,
    },
    commandRows: [
      row('qwen3-1.7b', 'Quicker', 'ok', 'Best for short ones.'),
      row(
        'qwen3-4b',
        'More accurate',
        advice.fourB,
        'Better with long ones that make several changes.',
      ),
    ],
    speech,
    estimateNote,
  };
}

// The model a choice resolves to on this phone: Auto's pick, or the one chosen. Null when voice isn't on offer or
// the choice can't be picked here.
export function chosenModel(view: ModelChoiceView, choice: ModelChoice): CommandModelId | null {
  if (view.auto === null) return null;
  if (choice === 'auto') return view.auto.model;
  const row = view.commandRows.find((r) => r.id === choice);
  return row === undefined || row.availability === 'blocked' ? null : row.id;
}

// What *Download and continue* fetches: the speech model and the chosen command model.
export function totalBytes(view: ModelChoiceView, choice: ModelChoice): number | null {
  const model = chosenModel(view, choice);
  if (model === null) return null;
  const row = view.commandRows.find((r) => r.id === model);
  return row === undefined ? null : view.speech.bytes + row.bytes;
}

// Decimal gigabytes with one decimal, as the mock-ups write them (1.1 GB, 3.1 GB).
export function formatGb(bytes: number): string {
  return `${(bytes / 1e9).toFixed(1)} GB`;
}
