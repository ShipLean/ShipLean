import type { Api, Executor, Pending, PreviewLine } from '../actions/types';
import type { BacklogAction } from './actions';
import { describeAddItem, reconcileAddItem, sendAddItem, undoAddItem } from './add/send';
import {
  describeAddNote,
  describeApprove,
  reconcileItem,
  sendAddNote,
  sendApprove,
  undoAddNote,
} from './item/send';
import { describeMove, reconcileMove, sendMove, undoMove } from './move/send';

type Kind = BacklogAction['kind'];

// The Pendings of one kind, with where each sits in the whole list.
function groupByKind(items: Pending[]): Map<string, { at: number; item: Pending }[]> {
  const groups = new Map<string, { at: number; item: Pending }[]>();
  items.forEach((item, at) => {
    const group = groups.get(item.action.kind) ?? [];
    group.push({ at, item });
    groups.set(item.action.kind, group);
  });
  return groups;
}

export const backlogExecutor: Executor<BacklogAction> = {
  area: 'backlog',
  send(a, api) {
    switch (a.kind) {
      case 'move':
        return sendMove(a, api);
      case 'addItem':
        return sendAddItem(a, api);
      case 'approve':
        return sendApprove(a, api);
      case 'addNote':
        return sendAddNote(a, api);
    }
  },
  describe(a): PreviewLine {
    switch (a.kind) {
      case 'move':
        return describeMove(a);
      case 'addItem':
        return describeAddItem(a);
      case 'approve':
        return describeApprove(a);
      case 'addNote':
        return describeAddNote(a);
    }
  },
  // An approval has no reverse (decisions A.2).
  reversible: (kind: Kind) => kind !== 'approve',
  undo(a, api) {
    switch (a.kind) {
      case 'move':
        return undoMove(a, api);
      case 'addItem':
        return undoAddItem(a, api);
      case 'addNote':
        return undoAddNote(a, api);
      case 'approve':
        return Promise.reject(new Error('An approval has no undo.'));
    }
  },
  async reconcile(items: Pending[], api: Api) {
    const outcomes: ('sent' | 'notSent')[] = items.map(() => 'notSent');
    for (const [kind, group] of groupByKind(items)) {
      const pending = group.map((g) => g.item);
      const answers = await reconcileKind(kind, pending, api);
      group.forEach((g, i) => {
        outcomes[g.at] = answers[i];
      });
    }
    return outcomes;
  },
};

function reconcileKind(kind: string, items: Pending[], api: Api) {
  switch (kind as Kind) {
    case 'move':
      return reconcileMove(items, api);
    case 'addItem':
      return reconcileAddItem(items, api);
    case 'approve':
    case 'addNote':
      return reconcileItem(items, api);
  }
}
