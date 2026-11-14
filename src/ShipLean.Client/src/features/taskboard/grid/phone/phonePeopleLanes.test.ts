import { describe, expect, it } from 'vitest';
import type { UserRef } from '@shiplean/core/api/refs';
import type { CardDto, TaskboardPersonRowDto } from '@shiplean/core/api/views';
import { phonePeopleLanes } from './phonePeopleLanes';

const user = (id: string, name = id): UserRef => ({ id, name }) as unknown as UserRef;
const card = (key: string, assignee: string | null): CardDto =>
  ({ id: key, key, assignee: assignee ? { id: assignee } : null }) as unknown as CardDto;
const personRow = (
  id: string | null,
  tasks: CardDto[],
  stateId = 'active',
): TaskboardPersonRowDto => ({
  person: id ? user(id) : null,
  taskCount: tasks.length,
  cells: tasks.length > 0 ? [{ stateId, tasks }] : [],
});

// The view holds Unassigned and the people with tasks, as the server builds it.
const VIEW_ROWS = [personRow(null, []), personRow('tom', [card('B2', 'tom'), card('D1', 'tom')])];
const MEMBERS = [user('priya', 'Priya'), user('tom', 'Tom'), user('mei', 'Mei')];
const ids = (lanes: { rowId: string }[]) => lanes.map((l) => l.rowId);

describe('phonePeopleLanes', () => {
  it('phonePeopleLanes_WhenMembersGiven_ShouldAddEveryoneAfterUnassigned', () => {
    const lanes = phonePeopleLanes(VIEW_ROWS, 'active', new Set(), MEMBERS);

    // The view's order for people with tasks, then everyone else by name.
    expect(ids(lanes)).toEqual(['unassigned', 'tom', 'mei', 'priya']);
    expect(lanes.map((l) => l.row.taskCount)).toEqual([0, 2, 0, 0]);
    expect(lanes[1].tasks).toHaveLength(2);
  });

  it('phonePeopleLanes_WhenViewOrdersPeopleDifferently_ShouldKeepTheViewsOrder', () => {
    const rows = [
      personRow(null, []),
      personRow('tom', [card('B2', 'tom')]),
      personRow('priya', [card('B1', 'priya')]),
    ];

    expect(ids(phonePeopleLanes(rows, 'active', new Set(), MEMBERS))).toEqual([
      'unassigned',
      'tom',
      'priya',
      'mei',
    ]);
  });

  it('phonePeopleLanes_WhenUnassignedMissingFromView_ShouldStillStartWithIt', () => {
    const lanes = phonePeopleLanes([personRow('tom', [card('B2', 'tom')])], 'active', new Set(), [
      user('tom', 'Tom'),
    ]);

    expect(ids(lanes)).toEqual(['unassigned', 'tom']);
  });

  it('phonePeopleLanes_WhenPersonWithTasksIsNoMember_ShouldKeepThemInTheViewsOrder', () => {
    const lanes = phonePeopleLanes(
      [...VIEW_ROWS, personRow('gone', [card('X1', 'gone')])],
      'active',
      new Set(),
      [user('tom', 'Tom'), user('ann', 'Ann')],
    );

    expect(ids(lanes)).toEqual(['unassigned', 'tom', 'gone', 'ann']);
  });

  it('phonePeopleLanes_WhenNoMembersGiven_ShouldKeepTheFeatureBoardLanes', () => {
    expect(ids(phonePeopleLanes(VIEW_ROWS, 'active', new Set()))).toEqual(['unassigned', 'tom']);
    expect(ids(phonePeopleLanes(VIEW_ROWS, 'active', new Set(), null))).toEqual([
      'unassigned',
      'tom',
    ]);
  });

  it('phonePeopleLanes_WhenFaceChosen_ShouldFilterMembersToo', () => {
    const tom = phonePeopleLanes(VIEW_ROWS, 'active', new Set(['tom']), MEMBERS);
    expect(tom.map((l) => [l.rowId, l.tasks.length])).toEqual([['tom', 2]]);

    const mei = phonePeopleLanes(VIEW_ROWS, 'active', new Set(['mei']), MEMBERS);
    expect(mei.map((l) => [l.rowId, l.row.taskCount, l.tasks.length])).toEqual([['mei', 0, 0]]);
  });

  it('phonePeopleLanes_WhenMemberHasNoTaskInThisState_ShouldKeepTheLaneWithNoTasks', () => {
    const lanes = phonePeopleLanes(VIEW_ROWS, 'new', new Set(), MEMBERS);

    expect(lanes).toHaveLength(4);
    expect(lanes.every((l) => l.tasks.length === 0)).toBe(true);
  });
});
