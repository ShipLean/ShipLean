import { describe, expect, test } from 'vitest';
import type { BoardSettingsContext } from '../panes';
import { emptySettings } from '../ct/boardSettingsFixtures';
import { columnsWarning } from './ColumnsPane';

const blocked = { id: 's1', name: 'Blocked', category: 'inProgress' } as const;
const open = { id: 's2', name: 'Open', category: 'proposed' } as const;
const gone = { id: 's3', name: 'Gone', category: 'removed' } as const;

function context(level: BoardSettingsContext['level'] = 'stories'): BoardSettingsContext {
  return {
    projectId: 'p',
    boardId: 'b',
    level,
    states: [{ type: 'story', states: [blocked, open, gone] }],
    warnings: [],
  };
}

describe('columnsWarning', () => {
  test('WhenColumnsMapNothing_ShouldCountEveryLiveState', () => {
    expect(columnsWarning(emptySettings(), context())).toBe('2 states not on any column');
  });

  test('WhenTheDraftMapsAState_ShouldNotCountIt', () => {
    const draft = {
      ...emptySettings(),
      columns: [
        {
          id: 'c1',
          name: 'Open',
          wipLimit: null,
          split: false,
          definitionOfDone: '',
          stateMappings: [{ type: 'story' as const, stateId: 's2' }],
        },
      ],
    };
    expect(columnsWarning(draft, context())).toBe('1 state not on any column');
  });

  test('WhenEveryLiveStateIsMapped_ShouldNotWarn', () => {
    const draft = {
      ...emptySettings(),
      columns: [
        {
          id: 'c1',
          name: 'All',
          wipLimit: null,
          split: false,
          definitionOfDone: '',
          stateMappings: [
            { type: 'story' as const, stateId: 's1' },
            { type: 'story' as const, stateId: 's2' },
          ],
        },
      ],
    };
    expect(columnsWarning(draft, context())).toBeNull();
  });

  test('WhenTheTaskboard_ShouldNeverWarn', () => {
    expect(columnsWarning(emptySettings(), context('taskboard'))).toBeNull();
  });
});

describe('columnsWarning dead mappings', () => {
  test('WhenAColumnMapsARemovedState_ShouldSaySo', () => {
    const draft = {
      ...emptySettings(),
      columns: [
        {
          id: 'c1',
          name: 'A',
          wipLimit: null,
          split: false,
          definitionOfDone: '',
          stateMappings: [{ type: 'story' as const, stateId: gone.id }],
        },
      ],
    };

    expect(columnsWarning(draft, context())).toBe(
      '2 states not on any column · 1 mapping to a removed state',
    );
  });
});
