import { confirmDecisions, getItemAsks } from '@shiplean/core/api/asks';
import { addComment, archiveComment, listComments } from '@shiplean/core/api/commentsApi';
import { getMe } from '@shiplean/core/api/me';
import type { Api, Pending, PreviewLine, SendReceipt } from '../../actions/types';
import type { BacklogAction, BacklogAddNote, BacklogApprove } from '../actions';

// The device clock that stamped Pending.createdAt and the server clock that stamped a comment can differ.
const CLOCK_SKEW_MS = 2 * 60 * 1000;

// The calls go through @shiplean/core, so the runner's api argument is unused here.
// Numbers no longer open are skipped by the server.
export async function sendApprove(a: BacklogApprove, api: Api): Promise<void> {
  void api;
  await confirmDecisions(a.itemId, a.numbers);
}

// "Approve D10 on SHIP-906". An approval has no undo: a decision changes later through Change on the web.
export function describeApprove(a: BacklogApprove): PreviewLine {
  return {
    id: `approve:${a.itemId}`,
    icon: 'check',
    text: `Approve ${a.numbers.map((n) => `D${n}`).join(', ')} on ${a.key}`,
    detail: `Posted on ${a.key}. The next agent to pick it up reads it.`,
    removable: true,
  };
}

// The note is posted word for word; the receipt is the comment's id, which Undo archives.
export async function sendAddNote(a: BacklogAddNote, api: Api): Promise<SendReceipt> {
  void api;
  const comment = await addComment(a.itemId, a.body);
  return { createdId: comment.id };
}

// `Note on SHIP-906: "text"`.
export function describeAddNote(a: BacklogAddNote): PreviewLine {
  return {
    id: `addNote:${a.itemId}`,
    icon: 'note',
    text: `Note on ${a.key}: "${a.body}"`,
    detail: `Posted on ${a.key}. The next agent to pick it up reads it.`,
    removable: true,
  };
}

// Archives the comment this send posted. Never guesses which comment it was.
export async function undoAddNote(
  a: BacklogAddNote,
  api: Api,
  receipt?: SendReceipt | null,
): Promise<void> {
  void api;
  if (receipt === undefined || receipt === null) {
    throw new Error(`The note on ${a.key} is not known, so it can't be undone.`);
  }
  await archiveComment(receipt.createdId);
}

// Whether an uncertain send went through. Pending.createdAt is the device's clock (ISO UTC).
async function reconcileOne(p: Pending, me: () => Promise<string>): Promise<'sent' | 'notSent'> {
  const a = p.action as BacklogAction;
  switch (a.kind) {
    case 'approve': {
      const asks = await getItemAsks(a.itemId);
      const open = new Set(asks.decisions.filter((d) => d.state === 'open').map((d) => d.number));
      return a.numbers.some((n) => open.has(n)) ? 'notSent' : 'sent';
    }
    case 'addNote': {
      const caller = await me();
      const since = Date.parse(p.createdAt) - CLOCK_SKEW_MS;
      const comments = await listComments(a.itemId);
      return comments.some(
        (c) => c.author?.id === caller && c.body === a.body && Date.parse(c.createdAt) >= since,
      )
        ? 'sent'
        : 'notSent';
    }
    default:
      return 'notSent';
  }
}

// One group of approve or addNote Pendings (the executor groups by kind).
export async function reconcileItem(items: Pending[], api: Api): Promise<('sent' | 'notSent')[]> {
  void api;
  // The caller is the same for the whole group: read it once, and only when a note needs it.
  let me: Promise<string> | null = null;
  const caller = () => (me ??= getMe().then((u) => u.id));
  return Promise.all(items.map((p) => reconcileOne(p, caller)));
}
