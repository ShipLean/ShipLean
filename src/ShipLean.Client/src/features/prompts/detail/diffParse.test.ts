import { expect, test } from 'vitest';
import patches from '../../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-patches.json';
import { promptDiffHunks, parsePromptPatch } from './diffParse';

const splitLines = (text: string) => (text === '' ? [] : text.replace(/\n$/, '').split('\n'));

test.each(patches.cases)('promptDiffHunks_WhenPinnedCase_ShouldParse $name', (pinned) => {
  const hunks = promptDiffHunks(pinned.patch);
  const header = pinned.patch.split('\n').filter((line) => line.startsWith('@@ '));
  expect(hunks).toHaveLength(header.length);

  const lines = hunks.flatMap((hunk) => hunk.lines);
  const from = splitLines(pinned.from);
  const to = splitLines(pinned.to);
  for (const line of lines) {
    if (line.kind !== 'add') expect(from[(line.oldLine ?? 0) - 1]).toBe(line.text);
    if (line.kind !== 'del') expect(to[(line.newLine ?? 0) - 1]).toBe(line.text);
    expect(line.oldLine === null).toBe(line.kind === 'add');
    expect(line.newLine === null).toBe(line.kind === 'del');
  }
  const removed = lines.filter((line) => line.kind === 'del').length;
  const added = lines.filter((line) => line.kind === 'add').length;
  expect(from.length - removed).toBe(to.length - added);
  expect(removed + added).toBeGreaterThan(0);
});

test('promptDiffHunks_WhenTwoHunksFarApart_ShouldReportTheSkippedLines', () => {
  const pinned = patches.cases.find((c) => c.name === 'twoHunksFarApart');
  const hunks = promptDiffHunks(pinned?.patch ?? '');
  expect(hunks.map((hunk) => hunk.skippedBefore)).toEqual([0, 17]);
});

test('parsePromptPatch_WhenEmpty_ShouldHaveNoFile', () => {
  expect(parsePromptPatch('')?.hunks ?? []).toHaveLength(0);
});
