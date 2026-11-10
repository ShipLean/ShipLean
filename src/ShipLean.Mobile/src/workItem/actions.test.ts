import { describe, expect, expectTypeOf, it } from 'vitest';
import type { AppAction } from '../actions/types';
import type { WorkItemAction } from './actions';

const base = { area: 'workItem', projectId: 'p1', itemId: 'i1', key: 'SHIP-1' } as const;

const everyKind: { [K in WorkItemAction['kind']]: Extract<WorkItemAction, { kind: K }> } = {
  setState: {
    ...base,
    kind: 'setState',
    revision: 1,
    stateId: 's2',
    stateName: 'Active',
    fromStateId: 's1',
    fromStateName: 'New',
  },
  assign: {
    ...base,
    kind: 'assign',
    revision: 1,
    userId: 'u1',
    name: 'Tom',
    fromUserId: null,
    fromName: null,
  },
  addNote: { ...base, kind: 'addNote', body: 'hi' },
  approve: { ...base, kind: 'approve', numbers: [1, 2] },
  discuss: { ...base, kind: 'discuss', number: 1, body: 'why' },
  reply: { ...base, kind: 'reply', number: 1, body: 'yes' },
  setHealth: { ...base, kind: 'setHealth', revision: 1, health: 'atRisk', from: null },
  confirmStatus: { ...base, kind: 'confirmStatus', revision: 1 },
  setStatus: { ...base, kind: 'setStatus', revision: 1, text: 'Built.', from: null },
};

describe('WorkItemAction', () => {
  it('every kind is a workItem AppAction', () => {
    // The typecheck is the real check (the mapped type and expectTypeOf); the runtime loop only reads back `base`.
    expectTypeOf<WorkItemAction>().toExtend<AppAction>();
    const actions: AppAction[] = Object.values(everyKind);
    expect(actions).toHaveLength(9);
    for (const [kind, action] of Object.entries(everyKind)) {
      expect(action.kind).toBe(kind);
      expect(action.area).toBe('workItem');
      expect(action.itemId).toBe('i1');
      expect(action.key).toBe('SHIP-1');
    }
  });
});
