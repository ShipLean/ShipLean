import type { BulkPlacement } from '@shiplean/core/api/backlogWrites';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { afterEach, describe, expect, it } from 'vitest';
import type { Api } from '../actions/types';
import { aPending } from '../test-support/actions';
import { aBacklogRow, aBacklogView } from '../test-support/backlog';
import { aConflict, aMe, aProject, aWorkItem, aWriteResult } from '../test-support/builders';
import { restoreFetch, type ApiTable, type FakeRequest } from '../test-support/fakeApi';
import { featuresApi, NOTE_ID } from '../test-support/features';
import { aComment } from '../test-support/workItem';
import type { FeaturesAction } from './actions';
import { featuresExecutor } from './executor';

afterEach(restoreFetch);

// The executor sends through @shiplean/core, which reads the installed fake at fetch; the api argument is unused.
const api = {} as Api;
const PROJECT = aProject().id;
const project = `/api/projects/${PROJECT}`;
const ITEM = 'item-id-906';
const KEY = 'SHIP-906';
const PATCH = `PATCH /api/work-items/${ITEM}`;
const BY_KEY = `GET ${project}/work-items/by-key/${KEY}`;
const BULK = `POST ${project}/backlog/bulk`;
const BACKLOG = `GET ${project}/backlogs/features`;

const base = { area: 'features' as const, itemId: ITEM, key: KEY };
const field = { ...base, projectId: PROJECT };
const bodyOf = (call: FakeRequest) =>
  call.body as { expectedRevision: number; changes: Record<string, unknown> };

// The item as the by-key read returns it now.
const itemNow = (revision: number, fields: Partial<WorkItemDto> = {}): WorkItemDto =>
  aWorkItem({ id: ITEM, key: KEY, revision, ...fields });
const inState = (id: string): Partial<WorkItemDto> => ({
  state: { id, name: id, category: 'inProgress' },
});

const setState: FeaturesAction = {
  ...field,
  kind: 'setState',
  revision: 7,
  stateId: 'state-active',
  stateName: 'Active',
  fromStateId: 'state-new',
  fromStateName: 'New',
  closing: false,
  openStories: 0,
};
const setHealth: FeaturesAction = {
  ...field,
  kind: 'setHealth',
  revision: 7,
  health: 'atRisk',
  from: 'onTrack',
};
const STATUS_TEXT = 'Blocked on the sign-in decision.';
const NOTE_BODY = 'The S22 runs hot after a minute';
const setStatus: FeaturesAction = {
  ...field,
  kind: 'setStatus',
  revision: 7,
  text: 'Blocked on the sign-in decision.',
  from: 'D1–D8 wait for Joel.',
};
const addNote: FeaturesAction = {
  ...base,
  kind: 'addNote',
  body: 'The S22 runs hot after a minute',
};

const place = (slot: BulkPlacement['slot'], beforeItemId: string | null = null): BulkPlacement => ({
  slot,
  beforeItemId,
});
const moveRank = (placement: BulkPlacement, undoPlacement: BulkPlacement): FeaturesAction => ({
  ...base,
  kind: 'moveRank',
  projectId: PROJECT,
  fromRank: 4,
  toRank: 2,
  placement,
  undoPlacement,
  shifted: [{ key: 'SHIP-743', from: 2, to: 3 }],
});

// The fake answers the overview's own Features; this item is not one of them, so its write routes are listed here.
const world = (table: ApiTable = {}) =>
  featuresApi(
    {
      [`POST /api/work-items/${ITEM}/decisions/confirm`]: { status: 204 },
      [`POST /api/work-items/${ITEM}/questions/2/answer`]: { status: 204 },
      [`POST /api/work-items/${ITEM}/comments`]: (request: FakeRequest) =>
        aComment(ITEM, { id: NOTE_ID, body: (request.body as { body: string }).body }),
      ...table,
    },
    { projectId: PROJECT },
  );
const send = (a: FeaturesAction) => featuresExecutor.send(a, api);
const undo = (a: FeaturesAction, receipt?: { createdId: string } | null) =>
  featuresExecutor.undo!(a, api, receipt);

describe('features executor', () => {
  it('patches state at the action revision', async () => {
    const fake = world({ [PATCH]: aWriteResult({ id: ITEM, key: KEY }) });
    await send(setState);
    const [call] = fake.callsTo(PATCH);
    expect(bodyOf(call).expectedRevision).toBe(7);
    expect(bodyOf(call).changes).toMatchObject({ state: { value: 'state-active' }, title: null });
    expect(bodyOf(call).changes.health).toBeUndefined();
    expect(bodyOf(call).changes.statusLine).toBeUndefined();
    expect(fake.callsTo(PATCH)).toHaveLength(1);
  });

  it('patches health with null for Not set', async () => {
    const fake = world({ [PATCH]: aWriteResult({ id: ITEM, key: KEY }) });
    await send({ ...setHealth, health: null, from: 'atRisk' } as FeaturesAction);
    await send(setHealth);
    const [cleared, set] = fake.callsTo(PATCH).map(bodyOf);
    expect(cleared.changes.health).toEqual({ value: null });
    expect(set.changes.health).toEqual({ value: 'atRisk' });
    expect(cleared.changes.state).toBeNull();
  });

  it('posts seen for markDigestSeen', async () => {
    const fake = world();
    await send({
      area: 'features',
      kind: 'markDigestSeen',
      itemId: null,
      key: null,
      projectId: PROJECT,
    });
    expect(fake.callsTo(`POST ${project}/features/digest/seen`)).toHaveLength(1);
  });

  it('undoes health only when it still holds the new value', async () => {
    // The send wrote atRisk; the item is now at revision 9 (a note moved it on).
    const fake = world({
      [PATCH]: aWriteResult({ id: ITEM, key: KEY }),
      [BY_KEY]: itemNow(9, { health: 'atRisk' }),
    });
    await undo(setHealth);
    const [call] = fake.callsTo(PATCH);
    expect(bodyOf(call).expectedRevision).toBe(9);
    expect(bodyOf(call).changes.health).toEqual({ value: 'onTrack' });

    // Someone set it to blocked since: nothing is written.
    const changed = world({ [BY_KEY]: itemNow(9, { health: 'blocked' }) });
    await expect(undo(setHealth)).rejects.toThrow(/changed since/);
    expect(changed.callsTo(PATCH)).toEqual([]);

    // A 409 on the undo PATCH is a failure, never retried.
    const stale = world({
      [PATCH]: aWriteResult(),
      [BY_KEY]: itemNow(8, { health: 'atRisk' }),
    });
    stale.failNext(PATCH, { status: 409, body: aConflict(aWorkItem({ id: ITEM })) });
    await expect(undo(setHealth)).rejects.toMatchObject({ status: 409 });
    expect(stale.callsTo(PATCH)).toHaveLength(1);

    // The same rule undoes a state change.
    const states = world({
      [PATCH]: aWriteResult(),
      [BY_KEY]: itemNow(8, inState('state-active')),
    });
    await undo(setState);
    expect(bodyOf(states.callsTo(PATCH)[0]).changes.state).toEqual({ value: 'state-new' });
    const moved = world({ [BY_KEY]: itemNow(8, inState('state-resolved')) });
    await expect(undo(setState)).rejects.toThrow(/changed since/);
    expect(moved.callsTo(PATCH)).toEqual([]);
  });

  it('fails the undo when the state differs at an unchanged revision', async () => {
    // A removed state's items are reassigned with no revision bump and no history row: the revision is the one the
    // send produced, yet the state is not the one it wrote.
    const fake = world({
      [PATCH]: aWriteResult({ id: ITEM, key: KEY }),
      [BY_KEY]: itemNow(8, inState('state-reassigned')),
    });
    await expect(undo(setState)).rejects.toThrow(/changed since/);
    expect(fake.callsTo(PATCH)).toEqual([]);
  });

  it('sends moveRank as a position-only bulkPlace and undoes it', async () => {
    const other = aBacklogRow({
      id: 'other',
      key: 'SHIP-743',
      rank: 3,
      revision: 4,
      type: { id: 'feature', name: 'Feature' },
    });
    const closed = aBacklogRow({
      id: 'closed',
      key: 'SHIP-742',
      rank: 1,
      type: { id: 'feature', name: 'Feature' },
    });
    const moved = aBacklogRow({
      id: ITEM,
      key: KEY,
      rank: 2,
      revision: 11,
      type: { id: 'feature', name: 'Feature' },
    });
    const level = (rows: ReturnType<typeof aBacklogRow>[]) => aBacklogView('features', rows);
    const fake = world({ [BACKLOG]: level([other, closed, moved]) });

    const a = moveRank(place('before', 'other'), place('before', 'next-after-old-spot'));
    await send(a);
    const [call] = fake.callsTo(BULK);
    expect(call.body).toEqual({
      items: [
        { id: ITEM, expectedRevision: 0, changes: null, placement: place('before', 'other') },
      ],
    });

    // The closed Feature sits above the moved one: places are compared, not rank numbers.
    await undo(a);
    const [, back] = fake.callsTo(BULK);
    expect(back.body).toEqual({
      items: [
        {
          id: ITEM,
          expectedRevision: 11,
          changes: null,
          placement: place('before', 'next-after-old-spot'),
        },
      ],
    });

    // Moved again since: the next ranked row is no longer the one it was placed before.
    const afterOther = aBacklogRow({ ...moved, rank: 4 });
    const gone = world({ [BACKLOG]: level([closed, other, afterOther]) });
    await expect(undo(a)).rejects.toThrow(/moved since/);
    expect(gone.callsTo(BULK)).toEqual([]);

    // A stale placement is thrown as a 409.
    const stale = world({ [BULK]: { written: [], stale: [aConflict(aWorkItem({ id: ITEM }))] } });
    await expect(send(a)).rejects.toMatchObject({ status: 409 });
    expect(stale.callsTo(BULK)).toHaveLength(1);
  });

  it('sends approve, answer, addNote and setStatus through their core writes', async () => {
    const fake = world({ [PATCH]: aWriteResult({ id: ITEM, key: KEY }) });
    await send({ ...base, kind: 'approve', numbers: [1, 2, 3] });
    await send({ ...base, kind: 'answer', number: 2, body: 'Parakeet, because it is faster' });
    const receipt = await send(addNote);
    await send(setStatus);

    expect(fake.callsTo(`POST /api/work-items/${ITEM}/decisions/confirm`)[0].body).toEqual({
      numbers: [1, 2, 3],
    });
    expect(fake.callsTo(`POST /api/work-items/${ITEM}/questions/2/answer`)[0].body).toEqual({
      body: 'Parakeet, because it is faster',
    });
    // The note is posted word for word and the receipt names it for Undo.
    expect(fake.callsTo(`POST /api/work-items/${ITEM}/comments`)[0].body).toEqual({
      body: 'The S22 runs hot after a minute',
    });
    expect(receipt).toEqual({ createdId: NOTE_ID });
    const status = bodyOf(fake.callsTo(PATCH)[0]);
    expect(status.expectedRevision).toBe(7);
    expect(status.changes).toEqual({ statusLine: { value: 'Blocked on the sign-in decision.' } });
  });

  it('undoes a note by archiving it and a status line by patching the previous one back', async () => {
    const fake = world({
      [PATCH]: aWriteResult({ id: ITEM, key: KEY }),
      [BY_KEY]: itemNow(9, { statusLine: STATUS_TEXT }),
    });
    await undo(addNote, { createdId: NOTE_ID });
    expect(fake.callsTo(`POST /api/comments/${NOTE_ID}/archive`)).toHaveLength(1);
    // No receipt: it fails, and archives nothing.
    await expect(undo(addNote, null)).rejects.toThrow(/not recorded/);
    expect(fake.callsTo(`POST /api/comments/${NOTE_ID}/archive`)).toHaveLength(1);

    await undo(setStatus);
    const [call] = fake.callsTo(PATCH);
    expect(bodyOf(call).expectedRevision).toBe(9);
    expect(bodyOf(call).changes).toEqual({ statusLine: { value: 'D1–D8 wait for Joel.' } });

    // A line that was edited by someone else since is left alone.
    const edited = world({
      [BY_KEY]: itemNow(9, { statusLine: 'Someone else wrote this.' }),
    });
    await expect(undo(setStatus)).rejects.toThrow(/changed since/);
    expect(edited.callsTo(PATCH)).toEqual([]);

    // A Feature that had no line has it cleared.
    const first = world({
      [PATCH]: aWriteResult(),
      [BY_KEY]: itemNow(9, { statusLine: STATUS_TEXT }),
    });
    await undo({ ...setStatus, from: null } as FeaturesAction);
    expect(bodyOf(first.callsTo(PATCH)[0]).changes).toEqual({ statusLine: { value: '' } });
  });

  it('is irreversible for approve, answer and markDigestSeen', async () => {
    for (const kind of ['approve', 'answer', 'markDigestSeen'] as const) {
      expect(featuresExecutor.reversible(kind)).toBe(false);
    }
    for (const kind of ['setState', 'setHealth', 'setStatus', 'moveRank', 'addNote'] as const) {
      expect(featuresExecutor.reversible(kind)).toBe(true);
    }
    const fake = world();
    await expect(undo({ ...base, kind: 'approve', numbers: [1] })).rejects.toThrow(/no reverse/);
    await expect(undo({ ...base, kind: 'answer', number: 1, body: 'x' })).rejects.toThrow(
      /no reverse/,
    );
    await expect(
      undo({
        area: 'features',
        kind: 'markDigestSeen',
        itemId: null,
        key: null,
        projectId: PROJECT,
      }),
    ).rejects.toThrow(/no reverse/);
    expect(fake.calls).toEqual([]);
  });

  it('words each kind, with the D8 warning on a close that leaves stories open', () => {
    const lines = (a: FeaturesAction) => featuresExecutor.describe(a);
    expect(lines(setState).text).toBe('SHIP-906: New → Active');
    expect(lines(setState).warning).toBeUndefined();
    const closing = lines({
      ...setState,
      stateName: 'Closed',
      closing: true,
      openStories: 3,
    } as FeaturesAction);
    expect(closing.warning).toBe('3 still open under it');
    expect(lines(setHealth).text).toBe('SHIP-906 health: On track → At risk');
    expect(lines({ ...setHealth, health: null } as FeaturesAction).text).toBe(
      'SHIP-906 health: On track → Not set',
    );
    expect(
      lines({
        area: 'features',
        kind: 'markDigestSeen',
        itemId: null,
        key: null,
        projectId: PROJECT,
      }).text,
    ).toBe('Mark the digest as seen');
    const move = lines(moveRank(place('first'), place('last')));
    expect(move.text).toBe('SHIP-906 4 → 2');
    expect(move.detail).toBe('SHIP-743 2 → 3');
    expect(lines({ ...base, kind: 'approve', numbers: [10] }).text).toBe('Approve D10 on SHIP-906');
    const answer = lines({ ...base, kind: 'answer', number: 1, body: 'x' });
    expect([answer.text, answer.detail]).toEqual([
      'Answer Q1 on SHIP-906',
      'Posted on SHIP-906. The next agent to pick it up reads it.',
    ]);
    expect(lines(addNote).text).toBe('Note on SHIP-906: "The S22 runs hot after a minute"');
    expect(lines(setStatus).text).toBe('New status: "Blocked on the sign-in decision."');
  });

  it('reconciles an uncertain send from the item itself', async () => {
    const pending = (action: FeaturesAction) =>
      aPending({ action, createdAt: '2030-01-01T09:00:00.000Z' });
    const reconcile = (...items: ReturnType<typeof pending>[]) =>
      featuresExecutor.reconcile!(items, api);
    world({ [BY_KEY]: itemNow(8, { health: 'atRisk' }) });
    // Health went through (the item holds it); state did not (the item is still in its old state).
    expect(await reconcile(pending(setHealth), pending(setState))).toEqual(['sent', 'notSent']);

    // An item that still holds the old health was not sent.
    world({ [BY_KEY]: itemNow(7, { health: 'onTrack' }) });
    expect(await reconcile(pending(setHealth))).toEqual(['notSent']);

    // A note is sent when the caller's own comment with that body came after it was dispatched.
    const me = aMe();
    world({
      [`GET /api/work-items/${ITEM}/comments`]: [
        aComment(ITEM, {
          body: NOTE_BODY,
          author: { ...me, photoUrl: null } as never,
          createdAt: '2030-01-01T09:00:30+00:00',
        }),
      ],
    });
    expect(await reconcile(pending(addNote))).toEqual(['sent']);
    world({ [`GET /api/work-items/${ITEM}/comments`]: [] });
    expect(await reconcile(pending(addNote))).toEqual(['notSent']);

    // The digest is idempotent: it is always sent again.
    expect(
      await reconcile(
        pending({
          area: 'features',
          kind: 'markDigestSeen',
          itemId: null,
          key: null,
          projectId: PROJECT,
        }),
      ),
    ).toEqual(['notSent']);
  });
});
