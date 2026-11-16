import { expect, test } from 'vitest';
import type { RunOverviewTurnDto, RunRowDto, RunTurnDto } from '@shiplean/core/api/inspection';
import { visibleTurns, zoomedAxis } from './runView';

const overview: RunOverviewTurnDto[] = [
  { turn: 1, startPct: 0, endPct: 50, hasError: false, waits: [] },
  { turn: 2, startPct: 80, endPct: 100, hasError: false, waits: [] },
];
const row = (id: string, name: string) => ({ id, name }) as RunRowDto;
const turns: RunTurnDto[] = [
  {
    number: 1,
    label: 'Turn 1',
    prompt: null,
    promptHidden: true,
    rows: [row('a', 'Edit'), row('b', 'Bash')],
  },
  { number: 2, label: 'Turn 2', prompt: null, promptHidden: true, rows: [row('c', 'Edit')] },
];

test('zoomedAxis_WhenTurnZoomed_ShouldNarrowToItsPercentages', () => {
  expect(zoomedAxis({ startMs: 0, endMs: 100000 }, overview, 2)).toEqual({
    startMs: 80000,
    endMs: 100000,
  });
});

test('zoomedAxis_WhenWholeRunOrUnknownTurn_ShouldKeepTheAxis', () => {
  const axis = { startMs: 0, endMs: 100000 };
  expect(zoomedAxis(axis, overview, null)).toBe(axis);
  expect(zoomedAxis(axis, overview, 9)).toBe(axis);
});

test('visibleTurns_WhenZoomedAndFound_ShouldKeepOneTurnAndMatchingRows', () => {
  const shown = visibleTurns(turns, 1, ' bash ');
  expect(shown.map((t) => t.number)).toEqual([1]);
  expect(shown[0].rows.map((r) => r.id)).toEqual(['b']);
  expect(visibleTurns(turns, null, '')).toEqual(turns);
});
