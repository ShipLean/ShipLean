import { beforeEach, describe, expect, it, vi } from 'vitest';
import { aPending } from '../test-support/actions';
import type { Api, Pending } from '../actions/types';
import type { BacklogAction } from './actions';
import { backlogExecutor } from './executor';
import * as add from './add/send';
import * as item from './item/send';
import * as move from './move/send';

vi.mock('./move/send');
vi.mock('./add/send');
vi.mock('./item/send');

const api = {} as Api;
const placement = { slot: 'first', beforeItemId: null } as const;
const actions: Record<BacklogAction['kind'], BacklogAction> = {
  move: {
    area: 'backlog',
    kind: 'move',
    itemId: 'i1',
    key: 'SHIP-1',
    revision: 1,
    level: 'features',
    fromRank: 2,
    toRank: 1,
    placement,
    undoPlacement: placement,
    shifted: [],
    warning: null,
  },
  addItem: {
    area: 'backlog',
    kind: 'addItem',
    itemId: null,
    key: null,
    projectId: 'p',
    type: 'story',
    title: 'T',
    parentId: null,
    parentKey: null,
    placement: null,
  },
  approve: { area: 'backlog', kind: 'approve', itemId: 'i2', key: 'SHIP-2', numbers: [1] },
  addNote: { area: 'backlog', kind: 'addNote', itemId: 'i3', key: 'SHIP-3', body: 'hello' },
};
const pending = (kind: BacklogAction['kind']): Pending =>
  aPending({ id: kind, action: actions[kind] });

beforeEach(() => {
  vi.resetAllMocks();
});

describe('backlogExecutor', () => {
  it('delegates each kind to its own sender', async () => {
    await backlogExecutor.send(actions.move, api);
    await backlogExecutor.send(actions.addItem, api);
    await backlogExecutor.send(actions.approve, api);
    await backlogExecutor.send(actions.addNote, api);
    expect(move.sendMove).toHaveBeenCalledExactlyOnceWith(actions.move, api);
    expect(add.sendAddItem).toHaveBeenCalledExactlyOnceWith(actions.addItem, api);
    expect(item.sendApprove).toHaveBeenCalledExactlyOnceWith(actions.approve, api);
    expect(item.sendAddNote).toHaveBeenCalledExactlyOnceWith(actions.addNote, api);

    backlogExecutor.describe(actions.move);
    backlogExecutor.describe(actions.addItem);
    backlogExecutor.describe(actions.approve);
    backlogExecutor.describe(actions.addNote);
    expect(move.describeMove).toHaveBeenCalledOnce();
    expect(add.describeAddItem).toHaveBeenCalledOnce();
    expect(item.describeApprove).toHaveBeenCalledOnce();
    expect(item.describeAddNote).toHaveBeenCalledOnce();

    await backlogExecutor.undo?.(actions.move, api);
    await backlogExecutor.undo?.(actions.addItem, api);
    await backlogExecutor.undo?.(actions.addNote, api);
    expect(move.undoMove).toHaveBeenCalledExactlyOnceWith(actions.move, api);
    expect(add.undoAddItem).toHaveBeenCalledExactlyOnceWith(actions.addItem, api);
    expect(item.undoAddNote).toHaveBeenCalledExactlyOnceWith(actions.addNote, api);
  });

  it('groups reconcile by kind and returns the answers in the order given', async () => {
    vi.mocked(move.reconcileMove).mockResolvedValue(['sent', 'notSent']);
    vi.mocked(add.reconcileAddItem).mockResolvedValue(['sent']);
    vi.mocked(item.reconcileItem)
      .mockResolvedValueOnce(['notSent'])
      .mockResolvedValueOnce(['sent']);
    const items = [
      pending('move'),
      pending('addItem'),
      pending('move'),
      pending('approve'),
      pending('addNote'),
    ];
    const answers = await backlogExecutor.reconcile?.(items, api);
    expect(move.reconcileMove).toHaveBeenCalledExactlyOnceWith([items[0], items[2]], api);
    expect(add.reconcileAddItem).toHaveBeenCalledExactlyOnceWith([items[1]], api);
    expect(item.reconcileItem).toHaveBeenCalledTimes(2);
    expect(answers).toEqual(['sent', 'sent', 'notSent', 'notSent', 'sent']);
  });

  it('marks move, addItem and addNote reversible and approve not', () => {
    expect(backlogExecutor.reversible('move')).toBe(true);
    expect(backlogExecutor.reversible('addItem')).toBe(true);
    expect(backlogExecutor.reversible('addNote')).toBe(true);
    expect(backlogExecutor.reversible('approve')).toBe(false);
  });
});
