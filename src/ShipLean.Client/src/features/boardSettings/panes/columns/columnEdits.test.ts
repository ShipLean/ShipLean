import { describe, expect, test } from 'vitest';
import type { BoardColumnJson } from '../../../../api/persisted';
import type { BoardTypeStatesDto } from '../../../../api/boards';
import {
  blankColumn,
  columnForState,
  deadMappings,
  fixFor,
  insertBeforeLast,
  moveColumn,
  parseWipLimit,
  unmappedStates,
} from './columnEdits';

const states: BoardTypeStatesDto[] = [
  {
    type: 'bug',
    states: [
      { id: 'b-new', name: 'New', category: 'proposed' },
      { id: 'b-triaged', name: 'Triaged', category: 'proposed' },
      { id: 'b-done', name: 'Closed', category: 'completed' },
      { id: 'b-gone', name: 'Removed', category: 'removed' },
    ],
  },
];

const column = (id: string, bug: string): BoardColumnJson => ({
  id,
  name: id,
  wipLimit: null,
  split: false,
  definitionOfDone: '',
  stateMappings: [{ type: 'bug', stateId: bug }],
});

describe('unmappedStates', () => {
  test('WhenAStateIsOnNoColumn_ShouldListItButNotRemovedOnes', () => {
    const columns = [column('a', 'b-new'), column('b', 'b-new'), column('c', 'b-done')];
    expect(unmappedStates(columns, states).map((u) => u.state.id)).toEqual(['b-triaged']);
  });
});

describe('fixFor', () => {
  test('WhenASameCategoryStateIsSharedByTwoColumns_ShouldRemapTheLastOfThem', () => {
    const columns = [column('a', 'b-new'), column('b', 'b-new'), column('c', 'b-done')];
    const fix = fixFor(columns, states, { type: 'bug', state: states[0].states[1] });
    expect(fix.kind === 'remap' && fix.column.id).toBe('b');
  });

  test('WhenNoColumnSharesAState_ShouldAddAColumn', () => {
    const columns = [column('a', 'b-new'), column('c', 'b-done')];
    expect(fixFor(columns, states, { type: 'bug', state: states[0].states[1] }).kind).toBe('add');
  });
});

describe('moveColumn', () => {
  const columns = ['first', 'a', 'b', 'c', 'last'].map((id) => column(id, 'b-new'));

  test('WhenAMiddleColumnMoves_ShouldReorderThem', () => {
    expect(moveColumn(columns, 1, 2).map((c) => c.id)).toEqual(['first', 'b', 'a', 'c', 'last']);
  });

  test('WhenTheFirstOrLastIsInvolved_ShouldChangeNothing', () => {
    expect(moveColumn(columns, 0, 1)).toBe(columns);
    expect(moveColumn(columns, 3, 4)).toBe(columns);
    expect(moveColumn(columns, 4, 3)).toBe(columns);
  });
});

describe('insertBeforeLast', () => {
  test('WhenAdded_ShouldGoBeforeTheLastColumn', () => {
    const columns = ['first', 'last'].map((id) => column(id, 'b-new'));
    expect(insertBeforeLast(columns, column('x', 'b-new')).map((c) => c.id)).toEqual([
      'first',
      'x',
      'last',
    ]);
  });
});

describe('columnForState', () => {
  test('WhenBuilt_ShouldBeNamedAfterTheStateAndMapIt', () => {
    const built = columnForState(states, { type: 'bug', state: states[0].states[1] });
    expect([built.name, built.stateMappings]).toEqual([
      'Triaged',
      [{ type: 'bug', stateId: 'b-triaged' }],
    ]);
  });
});

describe('parseWipLimit', () => {
  test('WhenBlankOrNotANumber_ShouldBeNone', () => {
    expect([parseWipLimit(''), parseWipLimit('x')]).toEqual([null, null]);
  });

  test('WhenBelowOne_ShouldBeOne', () => {
    expect(parseWipLimit('0')).toBe(1);
  });
});

describe('startingState', () => {
  test('WhenATypeHasNoLiveState_ShouldLeaveItOutOfANewColumn', () => {
    const none: BoardTypeStatesDto[] = [
      ...states,
      { type: 'story', states: [{ id: 's-gone', name: 'Gone', category: 'removed' }] },
    ];

    expect(blankColumn(none).stateMappings.map((m) => m.type)).toEqual(['bug']);
  });
});

describe('deadMappings', () => {
  test('WhenAColumnMapsARemovedOrMissingState_ShouldListIt', () => {
    const columns = [
      column('a', 'b-new'),
      column('b', 'b-gone'),
      { ...column('c', 'x'), stateMappings: [] },
    ];

    expect(deadMappings(columns, states).map((d) => d.column.id)).toEqual(['b', 'c']);
  });
});
