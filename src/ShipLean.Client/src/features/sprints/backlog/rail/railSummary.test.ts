import { expect, test } from 'vitest';
import type { StateCategory, UserRef } from '../../../../api/refs';
import type { WorkItemDto } from '../../../../api/workItems';
import { railSummary } from './railSummary';

const priya = { id: 'u1', name: 'Priya' } as UserRef;

const item = (
  key: string,
  type: 'story' | 'task' | 'bug',
  category: StateCategory,
  options: { assignee?: UserRef; points?: number } = {},
) =>
  ({
    id: key,
    key,
    title: `Title ${key}`,
    type: { id: type, name: type },
    state: { id: category, name: category, category },
    assignee: options.assignee ?? null,
    points: options.points ?? null,
  }) as unknown as WorkItemDto;

test('railSummary lists the unassigned, not started items, each story before its tasks and the loose tasks last', () => {
  const summary = railSummary(
    [
      {
        story: item('S1', 'story', 'proposed'),
        tasks: [item('T1', 'task', 'proposed'), item('T2', 'task', 'inProgress')],
      },
      { story: item('S2', 'bug', 'proposed', { assignee: priya }), tasks: [] },
    ],
    [item('T9', 'task', 'proposed')],
  );
  expect(summary.ready.map((r) => [r.item.key, r.sub])).toEqual([
    ['S1', 'S1'],
    ['T1', 'Title S1'],
    ['T9', 'T9'],
  ]);
});

test('railSummary counts what each person has in progress and ignores other categories and the unassigned', () => {
  const summary = railSummary(
    [
      {
        story: item('S1', 'story', 'inProgress', { assignee: priya }),
        tasks: [
          item('T1', 'task', 'inProgress', { assignee: priya }),
          item('T2', 'task', 'completed', { assignee: priya }),
          item('T3', 'task', 'inProgress'),
        ],
      },
    ],
    [],
  );
  expect(summary.activeByUser.get('u1')).toEqual(['Title S1', 'Title T1']);
  expect(summary.activeByUser.size).toBe(1);
});

test('railSummary sums story and bug points by category and leaves task points out', () => {
  const summary = railSummary(
    [
      {
        story: item('S1', 'story', 'inProgress', { points: 5 }),
        tasks: [item('T1', 'task', 'inProgress', { points: 3 })],
      },
      { story: item('S2', 'bug', 'inProgress', { points: 2 }), tasks: [] },
      { story: item('S3', 'story', 'completed', { points: 8 }), tasks: [] },
      { story: item('S4', 'story', 'proposed'), tasks: [] },
    ],
    [],
  );
  expect(summary.points).toEqual({ proposed: 0, inProgress: 7, resolved: 0, completed: 8 });
});
