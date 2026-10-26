import { expect, test } from 'vitest';
import type { StateCategory } from '../../../api/refs';
import type { WorkItemDto } from '../../../api/workItems';
import { doneTaskCount, sprintStories, unparentedTasks } from './sprintStories';

const item = (
  key: string,
  type: 'story' | 'bug' | 'task' | 'feature',
  sprintId: string | null,
  options: { children?: string[]; parent?: string | null; category?: StateCategory } = {},
) =>
  ({
    id: key,
    key,
    type: { id: type, name: type },
    sprint: sprintId === null ? null : { id: sprintId, name: sprintId, status: 'current' },
    parent: options.parent ? { id: options.parent } : null,
    state: { id: 's', name: 's', category: options.category ?? 'proposed' },
    children: (options.children ?? []).map((id) => ({ id })),
  }) as unknown as WorkItemDto;

test('sprintStories keeps the sprint stories and bugs in list order', () => {
  const items = [
    item('A', 'story', 's1'),
    item('B', 'story', 's2'),
    item('C', 'bug', 's1'),
    item('D', 'task', 's1'),
    item('E', 'feature', 's1'),
    item('F', 'story', null),
  ];
  expect(sprintStories(items, 's1').map((s) => s.story.key)).toEqual(['A', 'C']);
});

test('sprintStories gives each story its tasks as the full items from the list', () => {
  const task = item('T', 'task', 's1', { parent: 'A' });
  const [first] = sprintStories([item('A', 'story', 's1', { children: ['T'] }), task], 's1');
  expect(first!.tasks[0]).toBe(task);
});

test('sprintStories leaves out a task the list does not hold, which is how a Removed one goes', () => {
  const [first] = sprintStories(
    [
      item('A', 'story', 's1', { children: ['T', 'GONE'] }),
      item('T', 'task', 's1', { parent: 'A' }),
    ],
    's1',
  );
  expect(first!.tasks.map((t) => t.key)).toEqual(['T']);
});

test('unparentedTasks lists the sprint tasks with no parent and nothing else', () => {
  const items = [
    item('A', 'story', 's1', { children: ['T1'] }),
    item('T1', 'task', 's1', { parent: 'A' }),
    item('T2', 'task', 's1'),
    item('T3', 'task', 's2'),
    item('T4', 'task', null),
    item('B', 'bug', 's1'),
    item('T5', 'task', 's1'),
  ];
  expect(unparentedTasks(items, 's1').map((t) => t.key)).toEqual(['T2', 'T5']);
});

test('doneTaskCount counts the Completed-category tasks only', () => {
  const tasks = [
    item('T1', 'task', null, { category: 'completed' }),
    item('T2', 'task', null, { category: 'inProgress' }),
    item('T3', 'task', null, { category: 'completed' }),
  ];
  expect(doneTaskCount({ story: item('A', 'story', 's1'), tasks })).toBe(2);
});

test('sprintStories lists a story only the tasks that are in this sprint', () => {
  const [first] = sprintStories(
    [
      item('A', 'story', 's1', { children: ['T1', 'T2', 'T3'] }),
      item('T1', 'task', 's1', { parent: 'A' }),
      item('T2', 'task', 's2', { parent: 'A' }),
      item('T3', 'task', null, { parent: 'A' }),
    ],
    's1',
  );
  expect(first!.tasks.map((t) => t.key)).toEqual(['T1']);
});

test('unparentedTasks lists a sprint task whose parent is not a story or bug of this sprint', () => {
  const items = [
    item('A', 'story', 's1', { children: ['T1'] }),
    item('T1', 'task', 's1', { parent: 'A' }),
    item('B', 'story', 's2', { children: ['T2'] }),
    item('T2', 'task', 's1', { parent: 'B' }),
    item('C', 'story', null, { children: ['T3'] }),
    item('T3', 'task', 's1', { parent: 'C' }),
  ];
  expect(unparentedTasks(items, 's1').map((t) => t.key)).toEqual(['T2', 'T3']);
});
