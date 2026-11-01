import { expect, test } from 'vitest';
import type { SprintDto } from '../../../../api/sprints';
import { nextSprintOf } from './nextSprint';

const sprint = (name: string, status: SprintDto['status'], startDate: string) =>
  ({ id: name, name, status, startDate }) as SprintDto;

test('nextSprintOf_WhenSeveralFutureSprints_ShouldPickTheEarliestStart', () => {
  const sprints = [
    sprint('S24', 'current', '2026-10-05'),
    sprint('S26', 'future', '2026-11-16'),
    sprint('S25', 'future', '2026-11-02'),
  ];
  expect(nextSprintOf(sprints)?.name).toBe('S25');
});

test('nextSprintOf_WhenNoFutureSprint_ShouldBeUndefined', () => {
  expect(nextSprintOf([sprint('S24', 'current', '2026-10-05')])).toBeUndefined();
});
