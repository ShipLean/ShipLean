// Turns each registered screen's voice spec and test set into a screen pack: the phone's own system prompt, grammar
// and hotwords, and the rows to run them against. The pack is built only from the app's exports (commandPrompt.ts,
// resolve.ts), so the eval runs exactly what the phone runs. Rows that can't be right are rejected, not exported.
//
// Run from src/ShipLean.Mobile:
//   node --import ./tools/voice-eval/ts-hooks.mjs tools/voice-eval/export.ts [--out <dir>]
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import {
  buildGrammar,
  buildSystemPrompt,
  parseModelOutput,
  userTurn,
} from '../../src/voice/commandPrompt';
import type { ScreenVoiceSpec, TestSetRow, Transcript } from '../../src/voice/contracts';
import { MODEL_FILES, type ModelFile } from '../../src/voice/manifest';
import { copyNoteSpan } from '../../src/voice/resolve';
import { fits } from '../../src/voice/resolve/validate';
import { SCREENS, TESTSET_DIRS, testSetFiles, type ScreenRegistry } from './screens';

// A row as exported: the screen's context filled in when the row has none, and the user turn the phone would send
// for it (userTurn), so a harness outside TypeScript sends the same text.
export interface PackRow extends TestSetRow {
  context: Record<string, unknown>;
  userTurn: string;
}

export interface ScreenPack {
  screen: string;
  systemPrompt: string;
  grammar: string;
  hotwords: string[];
  rows: PackRow[];
}

export interface RejectedRow {
  file: string;
  line: number;
  problems: string[];
}

export interface ScreenReport {
  screen: string;
  rows: number;
  rejected: RejectedRow[];
  // Every action of the spec with fewer than five rows calling it.
  thinActions: { action: string; rows: number }[];
}

export interface ExportReport {
  screens: ScreenReport[];
  // Test sets whose screen isn't registered yet.
  unregistered: string[];
}

export const MIN_ROWS_PER_ACTION = 5;

const transcriptOf = (text: string): Transcript => ({ text, words: [], durationMs: 0 });

const isObject = (v: unknown): v is Record<string, unknown> =>
  typeof v === 'object' && v !== null && !Array.isArray(v);

function shapeProblems(row: unknown): string[] {
  if (!isObject(row)) return ['the row is not an object'];
  const problems: string[] = [];
  if (typeof row.screen !== 'string') problems.push('screen is not a string');
  if (typeof row.utterance !== 'string') problems.push('utterance is not a string');
  if (!Array.isArray(row.calls)) problems.push('calls is not an array');
  if (row.note !== null && typeof row.note !== 'string')
    problems.push('note is not a string or null');
  if (row.context !== undefined && !isObject(row.context))
    problems.push('context is not an object');
  return problems;
}

// What is wrong with one row for this spec; [] when it can be right. A call must name one of the spec's actions,
// be a shape the phone's parser reads, and fit the action's schema with its note copied from the utterance. The
// row's note must be the utterance's own words, exactly what copyNoteSpan takes from the call's noteSpan.
export function checkRow(spec: ScreenVoiceSpec, row: unknown): string[] {
  const shape = shapeProblems(row);
  if (shape.length > 0) return shape;
  const { screen, utterance, calls, note } = row as unknown as TestSetRow;
  const problems: string[] = [];
  if (screen !== spec.screen) problems.push(`screen "${screen}" is not "${spec.screen}"`);
  if (parseModelOutput(JSON.stringify(calls)).length !== calls.length)
    problems.push("calls are not a shape the phone's parser reads");
  if (note !== null && !utterance.includes(note))
    problems.push(`note "${note}" is not in the utterance word for word`);
  const transcript = transcriptOf(utterance);
  let notes = 0;
  calls.forEach((call, i) => {
    const action = spec.actions.find((a) => a.name === call.action);
    if (action === undefined) {
      problems.push(`call ${i}: action "${call.action}" is not in the spec`);
      return;
    }
    let args: Record<string, unknown> = call.args;
    if (action.noteArg !== undefined) {
      const copied = call.noteSpan === undefined ? null : copyNoteSpan(transcript, call.noteSpan);
      notes += 1;
      if (copied === null)
        problems.push(`call ${i}: noteSpan doesn't give a note from the utterance`);
      else if (copied !== note)
        problems.push(
          `call ${i}: noteSpan gives "${copied}", not the row's note "${String(note)}"`,
        );
      args = { ...args, [action.noteArg]: copied ?? '' };
    } else if (call.noteSpan !== undefined) {
      problems.push(`call ${i}: "${action.name}" takes no note`);
    }
    if (!fits(args, { type: 'object', ...action.args }))
      problems.push(
        `call ${i}: args ${JSON.stringify(call.args)} don't fit "${action.name}"'s schema`,
      );
  });
  if (note !== null && notes === 0) problems.push('the row has a note but no call takes one');
  return problems;
}

// One row per line; blank lines are skipped. A line that isn't JSON is kept as a string, so checkRow rejects it.
export function readTestSet(file: string): { line: number; row: unknown }[] {
  return readFileSync(file, 'utf8')
    .split('\n')
    .flatMap((text, i) => {
      if (text.trim() === '') return [];
      let row: unknown = text;
      try {
        row = JSON.parse(text);
      } catch {
        // Not JSON: checkRow reports it as not an object.
      }
      return [{ line: i + 1, row }];
    });
}

export function buildPack(
  spec: ScreenVoiceSpec,
  rows: { file: string; line: number; row: unknown }[],
): { pack: ScreenPack; report: ScreenReport } {
  const rejected: RejectedRow[] = [];
  const kept: PackRow[] = [];
  for (const { file, line, row } of rows) {
    const problems = checkRow(spec, row);
    if (problems.length > 0) {
      rejected.push({ file, line, problems });
      continue;
    }
    const r = row as TestSetRow;
    const context = r.context ?? spec.context();
    kept.push({ ...r, context, userTurn: userTurn(transcriptOf(r.utterance), context) });
  }
  const thinActions = spec.actions
    .map((a) => ({
      action: a.name,
      rows: kept.filter((r) => r.calls.some((c) => c.action === a.name)).length,
    }))
    .filter((a) => a.rows < MIN_ROWS_PER_ACTION);
  return {
    pack: {
      screen: spec.screen,
      systemPrompt: buildSystemPrompt(spec),
      grammar: buildGrammar(spec),
      hotwords: spec.hotwords(),
      rows: kept,
    },
    report: { screen: spec.screen, rows: kept.length, rejected, thinActions },
  };
}

// Writes out/<screen>.json for every registered screen and out/manifest.json (the pinned model files).
export function exportPacks(opts: {
  outDir: string;
  screens?: ScreenRegistry;
  testsetDirs?: string[];
  files?: ModelFile[];
}): ExportReport {
  const screens = opts.screens ?? SCREENS;
  const sets = testSetFiles(opts.testsetDirs ?? TESTSET_DIRS);
  mkdirSync(opts.outDir, { recursive: true });
  const reports = Object.entries(screens).map(([name, make]) => {
    const rows = sets
      .filter((s) => s.screen === name)
      .flatMap((s) => readTestSet(s.file).map((r) => ({ file: s.file, ...r })));
    const { pack, report } = buildPack(make(), rows);
    writeFileSync(join(opts.outDir, `${name}.json`), `${JSON.stringify(pack, null, 2)}\n`);
    return report;
  });
  writeFileSync(
    join(opts.outDir, 'manifest.json'),
    `${JSON.stringify(opts.files ?? MODEL_FILES, null, 2)}\n`,
  );
  const unregistered = [...new Set(sets.map((s) => s.screen))].filter((s) => !(s in screens));
  return { screens: reports, unregistered };
}

function print(report: ExportReport, outDir: string): void {
  for (const s of report.screens) {
    console.log(`${s.screen}: ${s.rows} rows exported to ${join(outDir, `${s.screen}.json`)}`);
    for (const r of s.rejected)
      console.log(`  rejected ${r.file}:${r.line}: ${r.problems.join('; ')}`);
    for (const a of s.thinActions)
      console.log(`  ${a.action} has ${a.rows} rows (fewer than ${MIN_ROWS_PER_ACTION})`);
  }
  for (const s of report.unregistered)
    console.log(`${s}: test set found, but the screen isn't registered in screens.ts`);
}

if (process.argv[1] !== undefined && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const at = process.argv.indexOf('--out');
  const outDir =
    at === -1 ? fileURLToPath(new URL('./out/', import.meta.url)) : process.argv[at + 1];
  const report = exportPacks({ outDir });
  print(report, outDir);
  if (report.screens.some((s) => s.rejected.length > 0)) process.exitCode = 1;
}
