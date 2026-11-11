import type { StateCategory } from '@shiplean/core/api/refs';
import type { CardDto, FeatureBoardRowDto, FeatureBoardViewDto } from '@shiplean/core/api/views';
import { describe, expect, it } from 'vitest';
import { blockedOnBoard, isBlocked, taskCounts } from './taskCounts';

const CATEGORIES: StateCategory[] = ['proposed', 'inProgress', 'resolved', 'completed', 'removed'];

const columns = CATEGORIES.map((category) => ({
  state: { id: `state-${category}`, name: category, category },
  count: 0,
}));

const aTask = (key: string, tags: string[] = [], owner: string | null = null): CardDto =>
  ({
    id: `id-${key}`,
    key,
    title: `Task ${key}`,
    tags: tags.map((name) => ({ name, colour: null })),
    assignee: owner === null ? null : { id: 'u', name: owner },
  }) as CardDto;

// Tasks by the category of the column they sit in.
const aRow = (tasks: Partial<Record<StateCategory, CardDto[]>>): FeatureBoardRowDto =>
  ({
    parent: { id: 'story' },
    cells: CATEGORIES.map((category) => ({
      stateId: `state-${category}`,
      tasks: tasks[category] ?? [],
    })),
  }) as unknown as FeatureBoardRowDto;

describe('taskCounts', () => {
  // Red when resolved counts as done, removed counts as to do, or completed tasks are counted.
  it('splits by state category and leaves removed out', () => {
    const row = aRow({
      proposed: [aTask('T1')],
      inProgress: [aTask('T2'), aTask('T3')],
      resolved: [aTask('T4')],
      completed: [aTask('T5'), aTask('T6')],
      removed: [aTask('T7')],
    });
    const counts = taskCounts(row, { columns });
    expect(counts.todo).toBe(1);
    expect(counts.inProgress).toBe(3);
    expect(counts.blocked).toEqual([]);
  });

  // Red when the tag is matched case-sensitively, a blocked task is counted twice, or a finished blocked task is named.
  it('marks a task blocked by its Blocked tag in any case', () => {
    const row = aRow({
      proposed: [aTask('T1', ['BLOCKED'])],
      inProgress: [aTask('T2', ['blocked'], 'Sam'), aTask('T3', ['Spike'])],
      completed: [aTask('T4', ['Blocked'])],
    });
    const counts = taskCounts(row, { columns });
    expect(counts.blocked).toEqual([
      { key: 'T1', title: 'Task T1', owner: null },
      { key: 'T2', title: 'Task T2', owner: 'Sam' },
    ]);
    expect(counts.todo).toBe(0);
    expect(counts.inProgress).toBe(1);
    expect(isBlocked(aTask('T5', ['Blocked']))).toBe(true);
    expect(isBlocked(aTask('T6', ['Spike']))).toBe(false);
  });

  it('collects the blocked tasks of every row on the board', () => {
    const board = {
      columns,
      rows: [aRow({ inProgress: [aTask('T1', ['Blocked'])] }), aRow({ proposed: [aTask('T2')] })],
    } as unknown as FeatureBoardViewDto;
    expect(blockedOnBoard(board).map((t) => t.key)).toEqual(['T1']);
  });
});
