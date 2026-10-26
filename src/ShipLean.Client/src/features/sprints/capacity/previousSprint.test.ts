import { expect, test } from 'vitest';
import type { SprintDto } from '../../../api/sprints';
import { previousSprint } from './previousSprint';

const sprint = (id: string, startDate: string) => ({ id, startDate }) as SprintDto;
const all = [
  sprint('s25', '2026-11-02'),
  sprint('s23', '2026-09-07'),
  sprint('s24', '2026-10-05'),
  sprint('s22', '2026-08-24'),
];

test('previousSprint_WhenEarlierSprintsExist_ShouldPickTheLatestStartBeforeThisOne', () => {
  expect(previousSprint(all, all[2]!)?.id).toBe('s23');
  expect(previousSprint(all, all[0]!)?.id).toBe('s24');
});

test('previousSprint_WhenFirst_ShouldBeUndefined', () => {
  expect(previousSprint(all, all[3]!)).toBeUndefined();
});
