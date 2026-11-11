// Scores the host eval's runs (run.py's out/runs/<date>/*.jsonl) into a Markdown report: per screen and action, the
// right action, the right arguments and the exact note, with CPU latency. It reads the model's raw text with the
// phone's own parseModelOutput and cuts notes with the phone's own copyNoteSpan, so nothing is scored that the phone
// would read differently. Run from src/ShipLean.Mobile (RUN.md):
//   node --import ./tools/voice-eval/ts-hooks.mjs --disable-warning=MODULE_TYPELESS_PACKAGE_JSON \
//     tools/voice-eval/score.ts <out>/runs/<date> [--out <file>]
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { isDeepStrictEqual } from 'node:util';
import { pathToFileURL } from 'node:url';
import { parseModelOutput } from '../../src/voice/commandPrompt';
import type { ParsedCall, Transcript } from '../../src/voice/contracts';
import { copyNoteSpan } from '../../src/voice/resolve';

// One line run.py appends per row. `transcript` is what the model was given: the utterance in the text tier,
// Parakeet's text in the audio tier. `output` is the model's raw reply.
export interface RunLine {
  row: number;
  screen: string;
  utterance: string;
  tier: 'text' | 'audio';
  model: string;
  transcript: string;
  output: string;
  llmMs: number;
  sttMs: number | null;
  expectedCalls: ParsedCall[];
  expectedNote: string | null;
  cpu: string;
  threads: number;
}

export interface ActionScore {
  action: string;
  rows: number;
  rightAction: number;
  rightArgs: number;
  // Rows that expect a note, and how many of those got it word for word.
  noteRows: number;
  exactNote: number;
  medianMs: number;
  p90Ms: number;
  flagged: boolean;
}

export interface RunScore {
  tier: string;
  model: string;
  screen: string;
  rows: number;
  actions: ActionScore[];
}

// Below this share of rows an action is flagged.
export const FLAG_BELOW = 0.9;

// A row with no expected call (a command the screen can't take) is scored under this name.
export const NO_ACTION = '(none)';

interface RowResult {
  actions: string[];
  rightAction: boolean;
  rightArgs: boolean;
  note: boolean | null;
  ms: number;
}

function transcriptOf(text: string): Transcript {
  return { text, words: [], durationMs: 0 };
}

// The note the phone would post: the transcript's own words at the model's noteSpan, never the model's text.
function noteFrom(line: RunLine, got: ParsedCall[]): boolean | null {
  if (line.expectedNote === null) return null;
  const at = line.expectedCalls.findIndex((c) => c.noteSpan !== undefined);
  const span = at === -1 ? undefined : got[at]?.noteSpan;
  if (span === undefined) return false;
  return copyNoteSpan(transcriptOf(line.transcript), span) === line.expectedNote;
}

function scoreRow(line: RunLine): RowResult {
  const got = parseModelOutput(line.output);
  const want = line.expectedCalls;
  const rightAction =
    got.length === want.length && got.every((c, i) => c.action === want[i].action);
  const rightArgs = rightAction && got.every((c, i) => isDeepStrictEqual(c.args, want[i].args));
  const names = [...new Set(want.map((c) => c.action))];
  return {
    actions: names.length === 0 ? [NO_ACTION] : names,
    rightAction,
    rightArgs,
    note: noteFrom(line, got),
    ms: line.llmMs + (line.sttMs ?? 0),
  };
}

// Nearest rank: the value at or below which the share p of the sorted values falls.
function percentile(sorted: number[], p: number): number {
  return sorted[Math.max(0, Math.ceil(p * sorted.length) - 1)];
}

function actionScore(action: string, results: RowResult[]): ActionScore {
  const ms = results.map((r) => r.ms).sort((a, b) => a - b);
  const noted = results.filter((r) => r.note !== null);
  const score = {
    action,
    rows: results.length,
    rightAction: results.filter((r) => r.rightAction).length,
    rightArgs: results.filter((r) => r.rightArgs).length,
    noteRows: noted.length,
    exactNote: noted.filter((r) => r.note === true).length,
    medianMs: percentile(ms, 0.5),
    p90Ms: percentile(ms, 0.9),
  };
  const shares = [score.rightAction / score.rows, score.rightArgs / score.rows];
  if (score.noteRows > 0) shares.push(score.exactNote / score.noteRows);
  return { ...score, flagged: shares.some((s) => s < FLAG_BELOW) };
}

// Groups by tier, model and screen, then by expected action. A row with several actions counts under each.
export function scoreRuns(lines: RunLine[]): RunScore[] {
  const runs = new Map<string, { line: RunLine; results: RowResult[] }>();
  for (const line of lines) {
    const id = `${line.tier}\n${line.model}\n${line.screen}`;
    const run = runs.get(id) ?? { line, results: [] };
    run.results.push(scoreRow(line));
    runs.set(id, run);
  }
  return [...runs.values()].map(({ line, results }) => {
    const names = [...new Set(results.flatMap((r) => r.actions))].sort();
    return {
      tier: line.tier,
      model: line.model,
      screen: line.screen,
      rows: results.length,
      actions: names.map((name) =>
        actionScore(
          name,
          results.filter((r) => r.actions.includes(name)),
        ),
      ),
    };
  });
}

function pct(n: number, of: number): string {
  return `${Math.round((100 * n) / of)} %`;
}

export function renderReport(date: string, lines: RunLine[]): string {
  const hosts = [...new Set(lines.map((l) => `${l.cpu}, ${l.threads} threads`))];
  const out = [
    `# Voice eval ${date}`,
    '',
    `Host CPU: ${hosts.join('; ')}.`,
    '',
    'These are CPU times on this host, not phone times: use them to compare models and screens, never as the latency a phone will show.',
    'Exact note counts only rows that expect a note: the transcript words at the model’s noteSpan, cut by the phone’s copyNoteSpan, must equal the row’s note.',
    `An action is flagged when its right action, right args or exact note share is under ${pct(FLAG_BELOW, 1)}.`,
  ];
  for (const run of scoreRuns(lines)) {
    out.push(
      '',
      `## ${run.screen} · ${run.tier} · ${run.model} (${run.rows} rows)`,
      '',
      '| Action | Rows | Right action | Right args | Exact note | Median ms | p90 ms | Flag |',
      '|---|---|---|---|---|---|---|---|',
    );
    for (const a of run.actions) {
      const note = a.noteRows === 0 ? '–' : `${pct(a.exactNote, a.noteRows)} of ${a.noteRows}`;
      out.push(
        `| ${a.action} | ${a.rows} | ${pct(a.rightAction, a.rows)} | ${pct(a.rightArgs, a.rows)} | ${note} | ${a.medianMs} | ${a.p90Ms} | ${a.flagged ? 'under 90 %' : ''} |`,
      );
    }
  }
  return out.join('\n') + '\n';
}

export function readRuns(dir: string): RunLine[] {
  return readdirSync(dir)
    .filter((f) => f.endsWith('.jsonl'))
    .sort()
    .flatMap((f) =>
      readFileSync(join(dir, f), 'utf8')
        .split('\n')
        .filter((l) => l.trim() !== '')
        .map((l) => JSON.parse(l) as RunLine),
    );
}

if (process.argv[1] !== undefined && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const dir = process.argv[2];
  if (dir === undefined) throw new Error('usage: score.ts <out>/runs/<date> [--out <file>]');
  const date = basename(dir);
  const at = process.argv.indexOf('--out');
  const file = at === -1 ? join(dir, '..', '..', `voice-eval-${date}.md`) : process.argv[at + 1];
  writeFileSync(file, renderReport(date, readRuns(dir)));
  console.log(`wrote ${file}`);
}
