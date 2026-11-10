import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { afterEach, describe, expect, it } from 'vitest';
import { buildGrammar, buildSystemPrompt, userTurn } from '../../src/voice/commandPrompt';
import type { TestSetRow } from '../../src/voice/contracts';
import { MODEL_FILES } from '../../src/voice/manifest';
import { exportPacks, type ScreenPack } from './export';
import { FIXTURE_SCREEN, fixtureData, fixtureVoiceSpec } from './fixtures/fixtureVoiceSpec';

const FIXTURES = fileURLToPath(new URL('./fixtures/', import.meta.url));
const screens = { [FIXTURE_SCREEN]: () => fixtureVoiceSpec(fixtureData) };

const dirs: string[] = [];
const tempDir = () => {
  const dir = mkdtempSync(join(tmpdir(), 'voice-eval-export-'));
  dirs.push(dir);
  return dir;
};
afterEach(() => {
  for (const dir of dirs.splice(0)) rmSync(dir, { recursive: true, force: true });
});

const readJson = <T>(file: string): T => JSON.parse(readFileSync(file, 'utf8')) as T;

const commentRow = (utterance: string, spoken: string, note: string): TestSetRow => {
  const start = utterance.indexOf(spoken);
  return {
    screen: FIXTURE_SCREEN,
    utterance,
    calls: [
      {
        action: 'comment',
        args: { key: 'SHIP-12' },
        noteSpan: { start, end: start + spoken.length },
      },
    ],
    note,
  };
};

describe('export', () => {
  it('writes a screen pack with the prompt and grammar from commandPrompt', () => {
    const out = tempDir();
    exportPacks({ outDir: out, screens, testsetDirs: [FIXTURES] });

    const spec = fixtureVoiceSpec(fixtureData);
    const pack = readJson<ScreenPack>(join(out, 'fixture.json'));
    expect(pack.screen).toBe('fixture');
    expect(pack.systemPrompt).toBe(buildSystemPrompt(spec));
    expect(pack.grammar).toBe(buildGrammar(spec));
    expect(pack.hotwords).toEqual(['SHIP-12', 'SHIP-40', 'Active', 'Done']);
    expect(pack.rows).toHaveLength(7);
    const first = pack.rows[0];
    expect(first.note).toBe('the build is green again');
    expect(first.context).toEqual(spec.context());
    expect(first.userTurn).toBe(
      userTurn({ text: first.utterance, words: [], durationMs: 0 }, spec.context()),
    );
    expect(readJson(join(out, 'manifest.json'))).toEqual(MODEL_FILES);
  });

  it("rejects a row whose action isn't in the spec, whose args fail the schema, or whose note isn't in the utterance", () => {
    const sets = tempDir();
    const good = commentRow(
      'comment on SHIP-12 the build is green',
      'the build is green',
      'the build is green',
    );
    const rows: unknown[] = [
      good,
      { ...good, calls: [{ action: 'archive', args: { key: 'SHIP-12' } }], note: null },
      {
        screen: FIXTURE_SCREEN,
        utterance: 'move SHIP-12 to closed',
        calls: [{ action: 'setState', args: { key: 'SHIP-12', state: 'Closed' } }],
        note: null,
      },
      // The note reworded: the span's words are in the utterance, the row's note isn't.
      commentRow('comment on SHIP-12 the build is green', 'the build is green', 'the build passes'),
      // The note is in the utterance, but the span copies other words.
      commentRow('comment on SHIP-12 the build is green', 'build is green', 'the build is green'),
    ];
    writeFileSync(join(sets, 'fixture.jsonl'), rows.map((r) => JSON.stringify(r)).join('\n'));

    const out = tempDir();
    const report = exportPacks({ outDir: out, screens, testsetDirs: [sets] });

    const [fixture] = report.screens;
    expect(fixture.rejected.map((r) => r.line)).toEqual([2, 3, 4, 5]);
    expect(fixture.rejected[0].problems).toEqual(['call 0: action "archive" is not in the spec']);
    expect(fixture.rejected[1].problems).toEqual([
      'call 0: args {"key":"SHIP-12","state":"Closed"} don\'t fit "setState"\'s schema',
    ]);
    expect(fixture.rejected[2].problems).toContain(
      'note "the build passes" is not in the utterance word for word',
    );
    expect(fixture.rejected[3].problems).toEqual([
      'call 0: noteSpan gives "build is green", not the row\'s note "the build is green"',
    ]);
    const pack = readJson<ScreenPack>(join(out, 'fixture.json'));
    expect(pack.rows.map((r) => r.utterance)).toEqual([good.utterance]);
  });

  it('lists actions with fewer than five rows', () => {
    const report = exportPacks({ outDir: tempDir(), screens, testsetDirs: [FIXTURES] });
    // Five comment rows, one setState row and one failure row.
    expect(report.screens).toEqual([
      { screen: 'fixture', rows: 7, rejected: [], thinActions: [{ action: 'setState', rows: 1 }] },
    ]);

    const sets = tempDir();
    writeFileSync(join(sets, 'fixture.jsonl'), '');
    const empty = exportPacks({ outDir: tempDir(), screens, testsetDirs: [sets] });
    expect(empty.screens[0].thinActions).toEqual([
      { action: 'comment', rows: 0 },
      { action: 'setState', rows: 0 },
    ]);
  });

  it('reports a test set whose screen is not registered', () => {
    const sets = tempDir();
    writeFileSync(join(sets, 'inbox.jsonl'), '');
    const report = exportPacks({ outDir: tempDir(), screens, testsetDirs: [sets] });
    expect(report.unregistered).toEqual(['inbox']);
  });
});
