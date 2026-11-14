import { expect, test } from 'vitest';
import { itemRow } from '../../ct/fixtures';
import { forecastLabels } from './forecastLabels';

const rows = ['SHIP-1', 'SHIP-2', 'SHIP-3', 'SHIP-4'].map((key) => itemRow({ key }));

test('forecastLabels_WhenRowsRunUpToAMark_ShouldLabelEveryRowOfTheRun', () => {
  const marks = new Map([
    [rows[1].id, 'Sprint 26'],
    [rows[2].id, 'Sprint 27'],
  ]);

  const labels = forecastLabels(rows, marks);

  expect([...labels]).toEqual([
    [rows[0].id, 'Sprint 26'],
    [rows[1].id, 'Sprint 26'],
    [rows[2].id, 'Sprint 27'],
  ]);
});

test('forecastLabels_WhenNoMarks_ShouldLabelNothing', () => {
  expect(forecastLabels(rows, new Map()).size).toBe(0);
});
