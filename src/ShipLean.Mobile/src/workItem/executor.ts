import {
  answerQuestion,
  confirmDecisions,
  discussDecision,
  getItemAsks,
} from '@shiplean/core/api/asks';
import { addComment, archiveComment, listComments } from '@shiplean/core/api/commentsApi';
import { getMe } from '@shiplean/core/api/me';
import { confirmStatus, setStatusLine } from '@shiplean/core/api/status';
import { patchItem } from '@shiplean/core/api/workItem';
import type { WorkItemChanges } from '@shiplean/core/api/workItems';
import type { Api, Executor, Pending, PreviewLine, SendReceipt } from '../actions/types';
import type { WorkItemAction } from './actions';

type Kind = WorkItemAction['kind'];

const REVERSIBLE: Record<Kind, boolean> = {
  setState: true,
  assign: true,
  addNote: true,
  setHealth: true,
  setStatus: true,
  // An approval, a discussion post, an answer and a status confirmation have no reverse (decisions A.2).
  approve: false,
  discuss: false,
  reply: false,
  confirmStatus: false,
};

const NO_CHANGES: WorkItemChanges = {
  title: null,
  description: null,
  acceptanceCriteria: null,
  reproSteps: null,
  severity: null,
  state: null,
  assignee: null,
  sprint: null,
  priority: null,
  points: null,
  parent: null,
  tags: null,
  addLinks: [],
  removeLinkIds: [],
};

// The device clock that stamped Pending.createdAt and the server clock that stamped a comment can differ.
const CLOCK_SKEW_MS = 2 * 60 * 1000;

const healthLabel = (health: Extract<WorkItemAction, { kind: 'setHealth' }>['health']): string =>
  health === 'onTrack'
    ? 'On track'
    : health === 'atRisk'
      ? 'At risk'
      : health === 'blocked'
        ? 'Blocked'
        : 'Not set';

const numbersLabel = (numbers: number[]) => numbers.map((n) => `D${n}`).join(', ');

async function send(a: WorkItemAction): Promise<SendReceipt | void> {
  switch (a.kind) {
    case 'setState':
      await patchItem(a.itemId, a.revision, { ...NO_CHANGES, state: { value: a.stateId } });
      return;
    case 'assign':
      await patchItem(a.itemId, a.revision, { ...NO_CHANGES, assignee: { value: a.userId } });
      return;
    case 'setHealth':
      await patchItem(a.itemId, a.revision, { ...NO_CHANGES, health: { value: a.health } });
      return;
    case 'setStatus':
      await setStatusLine(a.itemId, a.revision, a.text);
      return;
    case 'confirmStatus':
      await confirmStatus(a.itemId, a.revision);
      return;
    case 'addNote': {
      const comment = await addComment(a.itemId, a.body);
      return { createdId: comment.id };
    }
    case 'approve':
      return confirmDecisions(a.itemId, a.numbers);
    case 'discuss':
      return discussDecision(a.itemId, a.number, a.body);
    case 'reply':
      return answerQuestion(a.itemId, a.number, a.body);
  }
}

function describe(a: WorkItemAction): PreviewLine {
  switch (a.kind) {
    case 'setState':
      return {
        id: `workItem:${a.itemId}:setState`,
        icon: 'flag',
        text: `State: ${a.fromStateName} → ${a.stateName}`,
        detail: a.key,
        removable: true,
      };
    case 'assign':
      return {
        id: `workItem:${a.itemId}:assign`,
        icon: 'person',
        text: `Owner: ${a.fromName ?? 'No owner'} → ${a.name ?? 'No owner'}`,
        detail: a.key,
        removable: true,
      };
    case 'addNote':
      return {
        id: `workItem:${a.itemId}:addNote`,
        icon: 'note',
        text: `Note on ${a.key}: "${a.body}"`,
        removable: true,
      };
    case 'approve':
      return {
        id: `workItem:${a.itemId}:approve`,
        icon: 'check',
        text: `Approve ${numbersLabel(a.numbers)}`,
        detail: a.key,
        removable: true,
      };
    case 'discuss':
      return {
        id: `workItem:${a.itemId}:discuss:${a.number}`,
        icon: 'message',
        text: `Discuss D${a.number}`,
        detail: a.key,
        removable: true,
      };
    case 'reply':
      return {
        id: `workItem:${a.itemId}:reply:${a.number}`,
        icon: 'message',
        text: `Reply to Q${a.number}`,
        detail: `Posted on ${a.key}. The next agent to pick it up reads it.`,
        removable: true,
      };
    case 'setHealth':
      return {
        id: `workItem:${a.itemId}:setHealth`,
        icon: 'alert',
        text: `${a.key} health: ${healthLabel(a.from)} → ${healthLabel(a.health)}`,
        removable: true,
      };
    case 'confirmStatus':
      return {
        id: `workItem:${a.itemId}:confirmStatus`,
        icon: 'check',
        text: 'Status still true',
        detail: a.key,
        removable: true,
      };
    case 'setStatus':
      return {
        id: `workItem:${a.itemId}:setStatus`,
        icon: 'message',
        text: `New status: "${a.text}"`,
        detail: a.key,
        removable: true,
      };
  }
}

async function undo(a: WorkItemAction, receipt?: SendReceipt | null): Promise<void> {
  switch (a.kind) {
    case 'addNote':
      if (receipt === null || receipt === undefined)
        throw new Error('The note was not recorded, so it cannot be undone.');
      return archiveComment(receipt.createdId);
    case 'setState':
    case 'assign':
    case 'setHealth':
    case 'setStatus':
      // TODO: read the item with getItemByKey(projectId, a.key), fail unless the field still holds what the send
      // wrote, then PATCH the from value back at the item's current revision (a 409 fails too). WorkItemAction has no
      // projectId, so the read has no project to ask.
      throw new Error(`${a.key} can't be undone from here yet.`);
    case 'approve':
    case 'discuss':
    case 'reply':
    case 'confirmStatus':
      throw new Error(`A ${a.kind} has no reverse.`);
  }
}

// Whether an uncertain send went through. Pending.createdAt is the device's clock (ISO UTC).
async function reconcileOne(
  p: Pending,
  caller: () => Promise<string>,
): Promise<'sent' | 'notSent'> {
  const a = p.action as WorkItemAction;
  const since = Date.parse(p.createdAt) - CLOCK_SKEW_MS;
  const commentedBy = async (body: string): Promise<boolean> => {
    const me = await caller();
    const comments = await listComments(a.itemId);
    return comments.some(
      (c) => c.author?.id === me && c.body === body && Date.parse(c.createdAt) >= since,
    );
  };
  switch (a.kind) {
    case 'addNote':
      return (await commentedBy(a.body)) ? 'sent' : 'notSent';
    case 'discuss':
      return (await commentedBy(a.body)) ? 'sent' : 'notSent';
    case 'approve': {
      const asks = await getItemAsks(a.itemId);
      const open = new Set(asks.decisions.filter((d) => d.state === 'open').map((d) => d.number));
      return a.numbers.some((n) => open.has(n)) ? 'notSent' : 'sent';
    }
    case 'reply': {
      const asks = await getItemAsks(a.itemId);
      return asks.questions.some((q) => q.number === a.number && q.state === 'open')
        ? 'notSent'
        : 'sent';
    }
    case 'setState':
    case 'assign':
    case 'setHealth':
    case 'setStatus':
    case 'confirmStatus':
      // TODO: read the item with getItemByKey(projectId, a.key) and say 'sent' when it holds the change. WorkItemAction
      // has no projectId, so the read has no project to ask.
      throw new Error(`${a.key} can't be checked from here yet.`);
  }
}

export const workItemExecutor: Executor<WorkItemAction> = {
  area: 'workItem',
  // Everything goes through @shiplean/core, so the runner's api argument is unused. An error is the core's own.
  send: (a) => send(a),
  describe,
  reversible: (kind) => REVERSIBLE[kind],
  undo: (a, _api: Api, receipt) => undo(a, receipt),
  async reconcile(items) {
    let me: Promise<string> | null = null;
    const caller = () => (me ??= getMe().then((u) => u.id));
    return Promise.all(items.map((p) => reconcileOne(p, caller)));
  },
};
