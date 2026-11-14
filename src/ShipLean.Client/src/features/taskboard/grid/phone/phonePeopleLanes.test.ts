import { describe, expect, it } from 'vitest';
import type { UserRef } from '@shiplean/core/api/refs';
import type { CardDto, TaskboardPersonRowDto } from '@shiplean/core/api/views';
import { phonePeopleLanes } from './phonePeopleLanes';

const user = (id: string): UserRef => ({ id }) as unknown as UserRef;
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
const MEMBERS = [user('priya'), user('tom'), user('mei')];
const ids = (lanes: { rowId: string }[]) => lanes.map((l) => l.rowId);

describe('phonePeopleLanes', () => {
  it('PhonePeopleLanes_WhenEveryoneGiven_ShouldListEveryMemberWithUnassignedFirst', () => {
    const lanes = phonePeopleLanes(VIEW_ROWS, 'active', new Set(), MEMBERS);

    expect(ids(lanes)).toEqual(['unassigned', 'priya', 'tom', 'mei']);
    expect(lanes.map((l) => l.row.taskCount)).toEqual([0, 0, 2, 0]);
    expect(lanes[2].tasks).toHaveLength(2);
  });

  it('PhonePeopleLanes_WhenUnassignedMissingFromView_ShouldStillStartWithIt', () => {
    const lanes = phonePeopleLanes([personRow('tom', [card('B2', 'tom')])], 'active', new Set(), [
      user('tom'),
    ]);

    expect(ids(lanes)).toEqual(['unassigned', 'tom']);
  });

  it('PhonePeopleLanes_WhenPersonWithTasksIsNoMember_ShouldKeepThemAfterTheMembers', () => {
    const lanes = phonePeopleLanes(
      [...VIEW_ROWS, personRow('gone', [card('X1', 'gone')])],
      'active',
      new Set(),
      [user('tom')],
    );

    expect(ids(lanes)).toEqual(['unassigned', 'tom', 'gone']);
  });

  it('PhonePeopleLanes_WhenNoEveryone_ShouldKeepTheViewsRowsAsTheFeatureBoardHasThem', () => {
    expect(ids(phonePeopleLanes(VIEW_ROWS, 'active', new Set()))).toEqual(['unassigned', 'tom']);
    expect(ids(phonePeopleLanes(VIEW_ROWS, 'active', new Set(), null))).toEqual([
      'unassigned',
      'tom',
    ]);
  });

  it('PhonePeopleLanes_WhenFaceChosen_ShouldKeepOnlyThatPerson', () => {
    const tom = phonePeopleLanes(VIEW_ROWS, 'active', new Set(['tom']), MEMBERS);

    expect(tom.map((l) => [l.rowId, l.tasks.length])).toEqual([['tom', 2]]);
  });

  it('PhonePeopleLanes_WhenMemberHasNoTaskInThisState_ShouldKeepTheLaneWithNoTasks', () => {
    const lanes = phonePeopleLanes(VIEW_ROWS, 'new', new Set(), MEMBERS);

    expect(lanes).toHaveLength(4);
    expect(lanes.every((l) => l.tasks.length === 0)).toBe(true);
  });
});
