// The registry of screens the eval exports: each name maps to its voice spec, built from plain data by the screen's
// own pure factory (the fixture data its tests use). Test sets are never listed here: the exporter reads whatever
// <screen>.jsonl files the test-set folders hold.
import { existsSync, readdirSync } from 'node:fs';
import { basename, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { ScreenVoiceSpec } from '../../src/voice/contracts';
import { FIXTURE_SCREEN, fixtureData, fixtureVoiceSpec } from './fixtures/fixtureVoiceSpec';

export type ScreenRegistry = Record<string, () => ScreenVoiceSpec>;

// TODO: register the five real screens (Inbox, work item, Backlog, Features, Retro) from their voice spec factories
// and fixture data; the fixture screen stays as the exporter's own check.
export const SCREENS: ScreenRegistry = {
  [FIXTURE_SCREEN]: () => fixtureVoiceSpec(fixtureData),
};

// Where test sets live: each voice spec task's src/voice/testset/<screen>.jsonl, and the fixture screen's rows.
export const TESTSET_DIRS = [
  fileURLToPath(new URL('../../src/voice/testset/', import.meta.url)),
  fileURLToPath(new URL('./fixtures/', import.meta.url)),
];

export interface TestSetFile {
  screen: string;
  file: string;
}

// Every <screen>.jsonl in these folders, by screen name; a folder that doesn't exist yet holds none.
export function testSetFiles(dirs: string[]): TestSetFile[] {
  return dirs
    .flatMap((dir) =>
      (existsSync(dir) ? readdirSync(dir) : [])
        .filter((n) => n.endsWith('.jsonl'))
        .map((n) => ({ screen: basename(n, '.jsonl'), file: join(dir, n) })),
    )
    .sort((a, b) => a.screen.localeCompare(b.screen));
}
