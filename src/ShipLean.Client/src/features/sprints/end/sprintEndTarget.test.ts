import { expect, test } from 'vitest';
import type { SprintDto } from '../../../api/sprints';
import { sprintToWrapUp } from './sprintEndTarget';

const sprint = (
  id: string,
  status: SprintDto['status'],
  endDate: string,
  rolloverStatus: SprintDto['rolloverStatus'],
) => ({ id, status, endDate, rolloverStatus }) as SprintDto;

test('sprintToWrapUp picks the most recently ended sprint nobody has decided', () => {
  const sprints = [
    sprint('s22', 'past', '2026-09-18', null),
    sprint('s23', 'past', '2026-10-02', 'pending'),
    sprint('s24', 'current', '2026-10-30', null),
  ];
  expect(sprintToWrapUp(sprints)?.id).toBe('s23');
});

test('sprintToWrapUp skips decided sprints and sprints that have not ended', () => {
  const sprints = [
    sprint('s23', 'past', '2026-10-02', 'decided'),
    sprint('s24', 'current', '2026-10-30', null),
    sprint('s25', 'future', '2026-11-13', null),
  ];
  expect(sprintToWrapUp(sprints)).toBeUndefined();
});
