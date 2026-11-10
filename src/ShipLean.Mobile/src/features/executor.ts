import { confirmDecisions, answerQuestion, getItemAsks } from '@shiplean/core/api/asks';
import { bulkPlace, getBacklog, type BacklogFilter } from '@shiplean/core/api/backlog';
import type { BulkPlacement, BulkWriteResult } from '@shiplean/core/api/backlogWrites';
import { ApiRequestError } from '@shiplean/core/api/client';
import { addComment, archiveComment, listComments } from '@shiplean/core/api/commentsApi';
import { markDigestSeen } from '@shiplean/core/api/featuresOverview';
import { getHistory } from '@shiplean/core/api/history';
import { getMe } from '@shiplean/core/api/me';
import { setStatusLine } from '@shiplean/core/api/status';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { patchItem } from '@shiplean/core/api/workItem';
import type { FieldChange } from '@shiplean/core/api/persisted';
import type { RevisionDto, WorkItemChanges } from '@shiplean/core/api/workItems';
import type { Api, Executor, Pending, PreviewLine, SendReceipt } from '../actions/types';
import type { FeaturesAction } from './actions';

type Kind = FeaturesAction['kind'];
type Of<K extends Kind> = Extract<FeaturesAction, { kind: K }>;

const REVERSIBLE: Record<Kind, boolean> = {
  setState: true,
  setHealth: true,
  setStatus: true,
  moveRank: true,
  addNote: true,
  // An approval and an answer have no reverse (decisions A.2); marking the digest seen is not a change to undo.
  approve: false,
  answer: false,
  markDigestSeen: false,
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

// The whole Features level, closed Features included. A rank number depends on what the reader saw, so the move checks
// compare places, never numbers.
const LEVEL: BacklogFilter = { mode: 'list', filter: '', includeCompleted: true };

// The device clock that stamped Pending.createdAt and the server clock that stamped a comment can differ.
const CLOCK_SKEW_MS = 2 * 60 * 1000;

// A stale row is not an error to the bulk route; it is one to the runner, which turns the 409 into a conflict.
function refuseStale(result: BulkWriteResult): void {
  const [stale] = result.stale;
  if (stale === undefined) return;
  throw new ApiRequestError(stale.error, 409, 'conflict', stale);
}

async function place(a: Of<'moveRank'>, placement: BulkPlacement, expectedRevision: number) {
  refuseStale(
    await bulkPlace(a.projectId, [{ id: a.itemId, expectedRevision, changes: null, placement }]),
  );
}

// The moved Feature's row, if it still sits where the move put it: first, last, or just above the row it was placed before.
export function stillPlaced(
  rows: BacklogRowDto[],
  a: Pick<Of<'moveRank'>, 'itemId' | 'placement'>,
): BacklogRowDto | null {
  const ranked = rows
    .filter((r): r is BacklogRowDto & { rank: number } => r.rank !== null)
    .sort((x, y) => x.rank - y.rank);
  const i = ranked.findIndex((r) => r.id === a.itemId);
  if (i < 0) return null;
  const { slot, beforeItemId } = a.placement;
  const fits =
    slot === 'first'
      ? i === 0
      : slot === 'last'
        ? i === ranked.length - 1
        : ranked[i + 1]?.id === beforeItemId;
  return fits ? ranked[i] : null;
}

// The newest revision that changed the field (history is newest first), and the revision the item is at now.
interface FieldNow {
  revision: number;
  change: FieldChange | null;
  at: RevisionDto | null;
}

async function fieldNow(itemId: string, field: string): Promise<FieldNow> {
  const { revisions } = await getHistory(itemId);
  const at = revisions.find((r) => r.changes.some((c) => c.field === field)) ?? null;
  return {
    revision: revisions[0]?.rev ?? 0,
    change: at?.changes.find((c) => c.field === field) ?? null,
    at,
  };
}

// Undo of a field change. The send moved the revision, so read where the item is now: if the field no longer holds what
// the send wrote, someone changed it since and Undo writes nothing; otherwise put the old value back at the current
// revision (a 409 fails too, never a blind retry). The actions carry no project id, so the item's own history is read.
async function undoField<V>(
  itemId: string,
  key: string,
  field: string,
  sent: V,
  write: (revision: number) => Promise<unknown>,
): Promise<void> {
  const now = await fieldNow(itemId, field);
  if (now.change === null || now.change.to !== sent)
    throw new Error(`${key} has changed since, so the change can't be undone.`);
  await write(now.revision);
}

// Whether an uncertain field change went through: the newest revision that touched the field is after the one the
// action was built on, and wrote the new value.
async function fieldSent(itemId: string, field: string, revision: number, sent: unknown) {
  const now = await fieldNow(itemId, field);
  return now.at !== null && now.at.rev > revision && now.change?.to === sent;
}

const stories = (n: number) => `${n} ${n === 1 ? 'story' : 'stories'} still open`;

const healthLabel = (health: Of<'setHealth'>['health']): string =>
  health === 'onTrack'
    ? 'On track'
    : health === 'atRisk'
      ? 'At risk'
      : health === 'blocked'
        ? 'Blocked'
        : 'Not set';

async function send(a: FeaturesAction): Promise<SendReceipt | void> {
  switch (a.kind) {
    case 'setState':
      await patchItem(a.itemId, a.revision, { ...NO_CHANGES, state: { value: a.stateId } });
      return;
    case 'setHealth':
      await patchItem(a.itemId, a.revision, { ...NO_CHANGES, health: { value: a.health } });
      return;
    case 'markDigestSeen':
      return markDigestSeen(a.projectId);
    // One position-only placement: changes is null, so the server checks no revision and the 0 is never compared.
    case 'moveRank':
      return place(a, a.placement, 0);
    case 'approve':
      return confirmDecisions(a.itemId, a.numbers);
    case 'answer':
      return answerQuestion(a.itemId, a.number, a.body);
    case 'addNote': {
      const comment = await addComment(a.itemId, a.body);
      return { createdId: comment.id };
    }
    case 'setStatus':
      await setStatusLine(a.itemId, a.revision, a.text);
      return;
  }
}

function describe(a: FeaturesAction): PreviewLine {
  switch (a.kind) {
    case 'setState':
      return {
        id: `features:${a.itemId}:setState`,
        icon: 'flag',
        text: `${a.key}: ${a.fromStateName} → ${a.stateName}`,
        warning: a.closing && a.openStories > 0 ? stories(a.openStories) : undefined,
        removable: true,
      };
    case 'setHealth':
      return {
        id: `features:${a.itemId}:setHealth`,
        icon: 'alert',
        text: `${a.key} health: ${healthLabel(a.from)} → ${healthLabel(a.health)}`,
        removable: true,
      };
    case 'markDigestSeen':
      return {
        id: 'features:digest:seen',
        icon: 'check',
        text: 'Mark the digest as seen',
        removable: true,
      };
    case 'moveRank':
      return {
        id: `features:${a.itemId}:moveRank`,
        icon: 'rank',
        text: `${a.key} ${a.fromRank} → ${a.toRank}`,
        detail:
          a.shifted.length === 0
            ? undefined
            : a.shifted.map((s) => `${s.key} ${s.from} → ${s.to}`).join('\n'),
        removable: true,
      };
    case 'approve':
      return {
        id: `features:${a.itemId}:approve`,
        icon: 'check',
        text: `Approve ${a.numbers.map((n) => `D${n}`).join(', ')} on ${a.key}`,
        removable: true,
      };
    case 'answer':
      return {
        id: `features:${a.itemId}:answer:${a.number}`,
        icon: 'message',
        text: `Answer Q${a.number} on ${a.key}`,
        detail: `Posted on ${a.key}. The next agent to pick it up reads it.`,
        removable: true,
      };
    case 'addNote':
      return {
        id: `features:${a.itemId}:addNote`,
        icon: 'note',
        text: `Note on ${a.key}: "${a.body}"`,
        removable: true,
      };
    case 'setStatus':
      return {
        id: `features:${a.itemId}:setStatus`,
        icon: 'message',
        text: `New status: "${a.text}"`,
        removable: true,
      };
  }
}

async function undo(a: FeaturesAction, receipt?: SendReceipt | null): Promise<void> {
  switch (a.kind) {
    case 'setState':
      return undoField(a.itemId, a.key, 'state', a.stateId, (revision) =>
        patchItem(a.itemId, revision, { ...NO_CHANGES, state: { value: a.fromStateId } }),
      );
    case 'setHealth':
      return undoField(a.itemId, a.key, 'health', a.health, (revision) =>
        patchItem(a.itemId, revision, { ...NO_CHANGES, health: { value: a.from } }),
      );
    case 'setStatus':
      // A Feature that had no line has it cleared.
      return undoField(a.itemId, a.key, 'statusLine', a.text, (revision) =>
        setStatusLine(a.itemId, revision, a.from ?? ''),
      );
    case 'moveRank': {
      const view = await getBacklog(a.projectId, 'features', LEVEL);
      const row = stillPlaced(view.rows, a);
      if (row === null) throw new Error(`${a.key} has moved since, so the move can't be undone.`);
      return place(a, a.undoPlacement, row.revision);
    }
    case 'addNote':
      if (receipt === null || receipt === undefined)
        throw new Error('The note was not recorded, so it cannot be undone.');
      return archiveComment(receipt.createdId);
    case 'approve':
    case 'answer':
    case 'markDigestSeen':
      throw new Error(`A ${a.kind} has no reverse.`);
  }
}

// Whether an uncertain send went through. Pending.createdAt is the device's clock (ISO UTC).
async function reconcileOne(
  p: Pending,
  caller: () => Promise<string>,
): Promise<'sent' | 'notSent'> {
  const a = p.action as FeaturesAction;
  switch (a.kind) {
    // Idempotent, so sending it again is safe.
    case 'markDigestSeen':
      return 'notSent';
    case 'setState':
      return (await fieldSent(a.itemId, 'state', a.revision, a.stateId)) ? 'sent' : 'notSent';
    case 'setHealth':
      return (await fieldSent(a.itemId, 'health', a.revision, a.health)) ? 'sent' : 'notSent';
    case 'setStatus':
      return (await fieldSent(a.itemId, 'statusLine', a.revision, a.text)) ? 'sent' : 'notSent';
    case 'moveRank': {
      const view = await getBacklog(a.projectId, 'features', LEVEL);
      return stillPlaced(view.rows, a) === null ? 'notSent' : 'sent';
    }
    case 'approve': {
      const asks = await getItemAsks(a.itemId);
      const open = new Set(asks.decisions.filter((d) => d.state === 'open').map((d) => d.number));
      return a.numbers.some((n) => open.has(n)) ? 'notSent' : 'sent';
    }
    case 'answer': {
      const asks = await getItemAsks(a.itemId);
      return asks.questions.some((q) => q.number === a.number && q.state === 'open')
        ? 'notSent'
        : 'sent';
    }
    case 'addNote': {
      const me = await caller();
      const since = Date.parse(p.createdAt) - CLOCK_SKEW_MS;
      const comments = await listComments(a.itemId);
      return comments.some(
        (c) => c.author?.id === me && c.body === a.body && Date.parse(c.createdAt) >= since,
      )
        ? 'sent'
        : 'notSent';
    }
  }
}

export const featuresExecutor: Executor<FeaturesAction> = {
  area: 'features',
  // Everything goes through @shiplean/core, so the runner's api argument is unused. An error is the core's own.
  send: (a) => send(a),
  describe,
  reversible: (kind) => REVERSIBLE[kind],
  undo: (a, _api: Api, receipt) => undo(a, receipt),
  async reconcile(items) {
    // The caller is the same for the whole batch: read it once, and only when a note needs it.
    let me: Promise<string> | null = null;
    const caller = () => (me ??= getMe().then((u) => u.id));
    return Promise.all(items.map((p) => reconcileOne(p, caller)));
  },
};
