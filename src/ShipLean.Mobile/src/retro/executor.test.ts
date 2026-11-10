import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Api } from '../actions/types';
import { aPending } from '../test-support/actions';
import type { RetroAction, RetroKind } from './actions';
import * as mergeSend from './decide/mergeSend';
import * as decisionSend from './decide/send';
import * as pointSend from './add/send';
import { retroExecutor } from './executor';

vi.mock('./add/send', () => ({
  sendPoint: vi.fn(async () => undefined),
  describePoint: vi.fn(() => ({ id: 'point', icon: 'dot', text: 'point', removable: true })),
  undoPoint: vi.fn(async () => undefined),
  reconcilePoint: vi.fn(async (items: unknown[]) => items.map(() => 'sent')),
}));
vi.mock('./decide/send', () => ({
  sendDecision: vi.fn(async () => undefined),
  describeDecision: vi.fn(() => ({
    id: 'decision',
    icon: 'dot',
    text: 'decision',
    removable: true,
  })),
  undoDecision: vi.fn(async () => undefined),
  reconcileDecision: vi.fn(async (items: unknown[]) => items.map(() => 'notSent')),
}));
vi.mock('./decide/mergeSend', () => ({
  sendMerge: vi.fn(async () => undefined),
  describeMerge: vi.fn(() => ({ id: 'merge', icon: 'dot', text: 'merge', removable: true })),
  reconcileMerge: vi.fn(async (items: unknown[]) => items.map(() => 'sent')),
}));

const api = {} as Api;

const actions: Record<RetroKind, RetroAction> = {
  addPoint: {
    area: 'retro',
    kind: 'addPoint',
    itemId: null,
    key: null,
    projectId: 'p',
    sprintId: 's',
    category: 'improve',
    categoryGuessed: false,
    text: 'Profile first.',
  },
  support: { area: 'retro', kind: 'support', itemId: null, key: null, pointId: 'pt' },
  unsupport: { area: 'retro', kind: 'unsupport', itemId: null, key: null, pointId: 'pt' },
  newAction: {
    area: 'retro',
    kind: 'newAction',
    itemId: null,
    key: null,
    projectId: 'p',
    sprintId: 's',
    pointIds: ['pt'],
    title: 'Profile',
    ownerId: null,
    ownerName: null,
    themeId: null,
  },
  addToAction: {
    area: 'retro',
    kind: 'addToAction',
    itemId: 'wi',
    key: 'SHIP-9',
    projectId: 'p',
    pointIds: ['pt'],
  },
  notNeeded: {
    area: 'retro',
    kind: 'notNeeded',
    itemId: null,
    key: null,
    projectId: 'p',
    sprintId: 's',
    pointId: 'pt',
    reason: '',
  },
  mergeThemes: {
    area: 'retro',
    kind: 'mergeThemes',
    itemId: null,
    key: null,
    proposalId: 'm',
    fromName: 'a',
    intoName: 'b',
  },
  keepApart: {
    area: 'retro',
    kind: 'keepApart',
    itemId: null,
    key: null,
    proposalId: 'm',
    fromName: 'a',
    intoName: 'b',
  },
};

const senderOf: Record<RetroKind, 'point' | 'decision' | 'merge'> = {
  addPoint: 'point',
  support: 'point',
  unsupport: 'point',
  newAction: 'decision',
  addToAction: 'decision',
  notNeeded: 'decision',
  mergeThemes: 'merge',
  keepApart: 'merge',
};

const sends = {
  point: pointSend.sendPoint,
  decision: decisionSend.sendDecision,
  merge: mergeSend.sendMerge,
};
const describes = {
  point: pointSend.describePoint,
  decision: decisionSend.describeDecision,
  merge: mergeSend.describeMerge,
};

beforeEach(() => vi.clearAllMocks());

describe('retroExecutor', () => {
  it('delegates each kind to its own sender', async () => {
    for (const kind of Object.keys(actions) as RetroKind[]) {
      vi.clearAllMocks();
      const mine = senderOf[kind];
      await retroExecutor.send(actions[kind], api);
      retroExecutor.describe(actions[kind]);
      for (const sender of ['point', 'decision', 'merge'] as const) {
        expect(sends[sender], `send ${kind} -> ${sender}`).toHaveBeenCalledTimes(
          sender === mine ? 1 : 0,
        );
        expect(describes[sender], `describe ${kind} -> ${sender}`).toHaveBeenCalledTimes(
          sender === mine ? 1 : 0,
        );
      }
    }

    await retroExecutor.undo?.(actions.addPoint, api);
    await retroExecutor.undo?.(actions.notNeeded, api);
    expect(pointSend.undoPoint).toHaveBeenCalledTimes(1);
    expect(decisionSend.undoDecision).toHaveBeenCalledTimes(1);
  });

  it('reconciles each sender its own pendings and answers in the order given', async () => {
    const pending = (kind: RetroKind, id: string) =>
      aPending({ id, action: actions[kind], state: 'uncertain' });
    const answers = await retroExecutor.reconcile?.(
      [
        pending('mergeThemes', '1'),
        pending('support', '2'),
        pending('notNeeded', '3'),
        pending('addPoint', '4'),
      ],
      api,
    );
    expect(answers).toEqual(['sent', 'sent', 'notSent', 'sent']);
    expect(vi.mocked(pointSend.reconcilePoint).mock.calls[0][0].map((p) => p.id)).toEqual([
      '2',
      '4',
    ]);
    expect(vi.mocked(decisionSend.reconcileDecision).mock.calls[0][0].map((p) => p.id)).toEqual([
      '3',
    ]);
    expect(vi.mocked(mergeSend.reconcileMerge).mock.calls[0][0].map((p) => p.id)).toEqual(['1']);
  });

  it('marks addPoint, support, unsupport and notNeeded reversible and the rest not', () => {
    const reversible = (Object.keys(actions) as RetroKind[]).filter((k) =>
      retroExecutor.reversible(k),
    );
    expect(reversible.sort()).toEqual(['addPoint', 'notNeeded', 'support', 'unsupport']);
  });
});
