import { expect, test } from 'vitest';
import type { SprintDto } from '../../../api/sprints';
import { canCloseSprint } from './canCloseSprint';

const sprint = (
  status: SprintDto['status'],
  rolloverStatus: SprintDto['rolloverStatus'],
): SprintDto => ({
  id: 's',
  name: 'Sprint',
  startDate: '2026-10-12',
  endDate: '2026-10-23',
  goal: '',
  status,
  rolloverStatus,
  datesLabel: '',
  timeLabel: '',
});

test('canCloseSprint_WhenCurrentAndNotClosed_ShouldBeTrue', () => {
  expect(canCloseSprint(sprint('current', null))).toBe(true);
});

test('canCloseSprint_WhenEndedAndNotClosed_ShouldBeTrue', () => {
  expect(canCloseSprint(sprint('past', null))).toBe(true);
  expect(canCloseSprint(sprint('past', 'pending'))).toBe(true);
});

test('canCloseSprint_WhenFuture_ShouldBeFalse', () => {
  expect(canCloseSprint(sprint('future', null))).toBe(false);
});

test('canCloseSprint_WhenAlreadyClosed_ShouldBeFalse', () => {
  expect(canCloseSprint(sprint('past', 'decided'))).toBe(false);
  expect(canCloseSprint(sprint('current', 'decided'))).toBe(false);
});
