import { expect, test } from 'vitest';
import { planningView } from '../ct/fixtures';
import { forecastMarks } from './forecastMarks';

test('forecastMarks_WhenSprintsHaveCutItems_ShouldNameTheSprintByItemId', () => {
  const view = planningView();
  const backlog = view.sections[3].rows;
  expect(Object.fromEntries(forecastMarks(view.forecast))).toEqual({
    [backlog[1].id]: 'Sprint 26',
    [backlog[2].id]: 'Sprint 27',
  });
});

test('forecastMarks_WhenASprintHoldsNothing_ShouldDrawNoLine', () => {
  const view = planningView();
  const empty = { ...view.forecast!.sprints[0], lastItemId: null };
  expect(forecastMarks({ sprints: [empty] }).size).toBe(0);
  expect(forecastMarks(null).size).toBe(0);
});
