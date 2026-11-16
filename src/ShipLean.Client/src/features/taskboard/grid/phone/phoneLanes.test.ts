import { describe, expect, it } from 'vitest';
import type { StateRef } from '@shiplean/core/api/refs';
import type { CardDto, TaskboardRowDto } from '@shiplean/core/api/views';
import { phoneLanes } from './phoneLanes';
import { phonePeopleLanes } from './phonePeopleLanes';

const NEW: StateRef = { id: 'new', name: 'New', category: 'proposed' };
const ACTIVE: StateRef = { id: 'active', name: 'Active', category: 'inProgress' };
const OPEN_STORY: StateRef = { id: 's-active', name: 'Active', category: 'inProgress' };
const DONE_STORY: StateRef = { id: 's-closed', name: 'Closed', category: 'completed' };

const card = (key: string, state: StateRef, assignee: string | null = null): CardDto =>
  ({
    id: key,
    key,
    state,
    assignee: assignee ? { id: assignee } : null,
    rollups: [],
  }) as unknown as CardDto;

const row = (key: string, state: StateRef, tasks: CardDto[], closed = 0): TaskboardRowDto => ({
  parent: {
    ...card(key, state),
    rollups: [{ type: 'task', open: tasks.length, closed, inCurrentSprint: 0, inOtherSprint: 0 }],
  },
  offerResolve: false,
  cells: [NEW, ACTIVE].map((s) => ({
    stateId: s.id,
    tasks: tasks.filter((t) => t.state.id === s.id),
  })),
});

// Backlog order: A (closed), B, C (closed), D.
const ROWS = [
  row('A', DONE_STORY, [], 3),
  row('B', OPEN_STORY, [card('B1', NEW, 'priya'), card('B2', ACTIVE, 'tom')]),
  row('C', DONE_STORY, [], 1),
  row('D', OPEN_STORY, [card('D1', ACTIVE, 'tom')], 2),
];
const keys = (lanes: { row: TaskboardRowDto }[]) => lanes.map((l) => l.row.parent?.key);

const UNPARENTED = (tasks: CardDto[]): TaskboardRowDto => ({
  parent: null,
  offerResolve: false,
  cells: [NEW, ACTIVE].map((s) => ({
    stateId: s.id,
    tasks: tasks.filter((t) => t.state.id === s.id),
  })),
});

describe('phoneLanes', () => {
  it('phoneLanes_WhenUnparentedRowIsNotLast_ShouldPutItLast', () => {
    const rows = [ROWS[1], UNPARENTED([card('U1', NEW)]), ROWS[3]];
    expect(keys(phoneLanes(rows, 'new', new Set()).lanes)).toEqual(['B', 'D', undefined]);
    expect(phoneLanes(rows, 'new', new Set()).lanes.map((l) => l.rowId)).toEqual([
      'B',
      'D',
      'unparented',
    ]);
  });

  it('phoneLanes_WhenUnparentedRowHasNoTasks_ShouldKeepItAsALane', () => {
    const lanes = phoneLanes([ROWS[1], UNPARENTED([])], 'active', new Set()).lanes;
    expect(lanes.map((l) => l.rowId)).toEqual(['B', 'unparented']);
    expect(lanes[1].tasks).toEqual([]);
  });

  it('phoneLanes_WhenNoUnparentedRow_ShouldNotAddOne', () => {
    const lanes = phoneLanes(ROWS, 'new', new Set()).lanes;
    expect(lanes.map((l) => l.rowId)).not.toContain('unparented');
  });

  it('PhoneLanes_WhenAnyState_ShouldKeepOpenStoriesInBacklogOrderOnEveryPage', () => {
    expect(keys(phoneLanes(ROWS, 'new', new Set()).lanes)).toEqual(['B', 'D']);
    expect(keys(phoneLanes(ROWS, 'active', new Set()).lanes)).toEqual(['B', 'D']);
  });

  it('PhoneLanes_WhenStoryHasNoTaskInState_ShouldKeepItsLaneEmpty', () => {
    const lanes = phoneLanes(ROWS, 'new', new Set()).lanes;
    expect(lanes.map((l) => l.tasks.map((t) => t.key))).toEqual([['B1'], []]);
    expect(lanes[1].hidden).toBe(0);
  });

  it('PhoneLanes_WhenStoriesClosed_ShouldListThemInBacklogOrderWithDoneOfTotal', () => {
    const { closed } = phoneLanes(ROWS, 'new', new Set());
    expect(keys(closed)).toEqual(['A', 'C']);
    expect(closed.map((c) => `${c.done}/${c.total}`)).toEqual(['3/3', '1/1']);
  });

  it('PhoneLanes_WithAClosedStoryBetweenOpenOnes_ShouldKeepBacklogOrderAndUnparentedLast', () => {
    const loose: TaskboardRowDto = { parent: null, offerResolve: false, cells: [] };
    // Unparented arrives first in the input; A and C are closed, between and before the open B and D.
    const { entries } = phoneLanes([loose, ...ROWS], 'new', new Set());
    expect(
      entries.map((e) => `${e.kind}:${e.kind === 'lane' ? e.lane.rowId : e.story.rowId}`),
    ).toEqual(['closed:A', 'lane:B', 'closed:C', 'lane:D', 'lane:unparented']);
  });

  it('PhoneLanes_WhenFilterHidesTasks_ShouldCountTheHiddenOnes', () => {
    const lanes = phoneLanes(ROWS, 'active', new Set(['priya'])).lanes;
    expect(lanes.map((l) => [l.tasks.length, l.hidden])).toEqual([
      [0, 1],
      [0, 1],
    ]);
  });

  it('PhoneLanes_WhenUnparentedRow_ShouldBeALaneCalledUnparented', () => {
    const loose: TaskboardRowDto = { parent: null, offerResolve: false, cells: [] };
    expect(phoneLanes([...ROWS, loose], 'new', new Set()).lanes.map((l) => l.rowId)).toEqual([
      'B',
      'D',
      'unparented',
    ]);
  });
});

describe('phonePeopleLanes', () => {
  const people = [
    { person: null, taskCount: 0, cells: [] },
    {
      person: { id: 'tom' },
      taskCount: 2,
      cells: [{ stateId: 'active', tasks: [card('B2', ACTIVE, 'tom'), card('D1', ACTIVE, 'tom')] }],
    },
  ] as unknown as Parameters<typeof phonePeopleLanes>[0];

  it('PhonePeopleLanes_WhenFaceChosen_ShouldKeepOnlyThatPerson', () => {
    expect(phonePeopleLanes(people, 'active', new Set()).map((l) => l.rowId)).toEqual([
      'unassigned',
      'tom',
    ]);
    const tom = phonePeopleLanes(people, 'active', new Set(['tom']));
    expect(tom.map((l) => [l.rowId, l.tasks.length])).toEqual([['tom', 2]]);
  });
});
