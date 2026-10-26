import { expect, test } from 'vitest';
import type { SprintDto } from '../../../api/sprints';
import { orderSprints } from './orderSprints';

const sprint = (name: string, status: SprintDto['status'], startDate: string): SprintDto => ({
  id: name,
  name,
  startDate,
  endDate: startDate,
  goal: '',
  status,
  rolloverStatus: null,
  datesLabel: '',
  timeLabel: '',
});

test('orderSprints_WhenMixed_ShouldPutCurrentThenFutureThenPast', () => {
  const ordered = orderSprints([
    sprint('S22', 'past', '2026-09-07'),
    sprint('S26', 'future', '2026-11-02'),
    sprint('S23', 'past', '2026-09-21'),
    sprint('S25', 'future', '2026-10-19'),
    sprint('S24', 'current', '2026-10-05'),
  ]);
  expect(ordered.map((s) => s.name)).toEqual(['S24', 'S25', 'S26', 'S23', 'S22']);
});
