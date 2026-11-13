import { describe, expect, it } from 'vitest';
import type { StateRef } from '@shiplean/core/api/refs';
import type { CardDto } from '@shiplean/core/api/views';
import { stateChips } from './stateChips';

const NEW: StateRef = { id: 'new', name: 'New', category: 'proposed' };
const ACTIVE: StateRef = { id: 'active', name: 'Active', category: 'inProgress' };
const REMOVED: StateRef = { id: 'removed', name: 'Removed', category: 'removed' };

const card = (assignee: string | null) =>
  ({ assignee: assignee ? { id: assignee } : null }) as unknown as CardDto;

const COLUMNS = [NEW, ACTIVE, REMOVED].map((state) => ({ state }));
const ROWS = [
  {
    cells: [
      { stateId: 'new', tasks: [card('priya'), card(null)] },
      { stateId: 'active', tasks: [card('tom')] },
    ],
  },
  { cells: [{ stateId: 'new', tasks: [card('tom')] }] },
];

describe('stateChips', () => {
  it('StateChips_WhenNoFace_ShouldCountEveryTaskPerStateAndLeaveRemovedOut', () => {
    expect(stateChips(COLUMNS, ROWS, new Set())).toEqual([
      { id: 'new', label: 'New', count: 3 },
      { id: 'active', label: 'Active', count: 1 },
    ]);
  });

  it('StateChips_WhenFacesChosen_ShouldCountOnlyTheirTasks', () => {
    expect(stateChips(COLUMNS, ROWS, new Set(['tom'])).map((c) => c.count)).toEqual([1, 1]);
    expect(stateChips(COLUMNS, ROWS, new Set(['unassigned'])).map((c) => c.count)).toEqual([1, 0]);
  });
});
