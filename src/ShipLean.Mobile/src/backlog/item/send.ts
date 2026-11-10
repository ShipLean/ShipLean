import type { Api, Pending, PreviewLine } from '../../actions/types';
import type { BacklogAddNote, BacklogApprove } from '../actions';

export async function sendApprove(a: BacklogApprove, api: Api): Promise<void> {
  void a;
  void api;
  throw new Error('TODO: confirm the decisions');
}

export function describeApprove(a: BacklogApprove): PreviewLine {
  // TODO: the design's wording.
  return {
    id: `approve:${a.itemId}`,
    icon: 'check',
    text: `Approve ${a.numbers.map((n) => `D${n}`).join(', ')} on ${a.key}`,
    removable: true,
  };
}

export async function sendAddNote(a: BacklogAddNote, api: Api): Promise<void> {
  void a;
  void api;
  throw new Error('TODO: post the comment');
}

export function describeAddNote(a: BacklogAddNote): PreviewLine {
  // TODO: the design's wording.
  return {
    id: `addNote:${a.itemId}`,
    icon: 'note',
    text: `Note on ${a.key}`,
    detail: a.body,
    removable: true,
  };
}

export async function undoAddNote(a: BacklogAddNote, api: Api): Promise<void> {
  void a;
  void api;
  throw new Error('TODO: archive the comment');
}

// One group of approve or addNote Pendings (the executor groups by kind).
export async function reconcileItem(items: Pending[], api: Api): Promise<('sent' | 'notSent')[]> {
  void api;
  // TODO: reload the item's asks or comments and compare.
  return items.map(() => 'notSent');
}
