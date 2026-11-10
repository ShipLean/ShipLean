import { ApiRequestError } from '@shiplean/core/api/client';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { afterEach, describe, expect, it } from 'vitest';
import type { Api, SendReceipt } from '../actions/types';
import { aPending } from '../test-support/actions';
import { aConflict, aMe, aProject } from '../test-support/builders';
import { restoreFetch, type FakeRequest } from '../test-support/fakeApi';
import {
  aComment,
  aDecision,
  aFeatureItem,
  anItemAsks,
  aQuestion,
  NOTE_IDS,
  workItemApi,
} from '../test-support/workItem';
import type { WorkItemAction } from './actions';
import { workItemExecutor } from './executor';

afterEach(restoreFetch);

// The executor sends through @shiplean/core, which reads the installed fake at fetch; the api argument is unused.
const api = {} as Api;
const PROJECT = aProject().id;
const feature = aFeatureItem({ projectId: PROJECT, revision: 7 });
const ITEM = feature.id;
const KEY = feature.key;
const PATCH = `PATCH /api/work-items/${ITEM}`;
const base = { area: 'workItem' as const, projectId: PROJECT, itemId: ITEM, key: KEY };
const bodyOf = (call: FakeRequest) =>
  call.body as { expectedRevision: number; changes: Record<string, unknown> };

const world = () => workItemApi({}, { projectId: PROJECT, items: [feature] });
// The item as the by-key read returns it now: its revision and the fields the test sets.
const now = (revision: number, overrides: Partial<WorkItemDto> = {}) =>
  workItemApi({}, { projectId: PROJECT, items: [{ ...feature, revision, ...overrides }] });
const inState = (id: string) => ({ state: { ...feature.state, id } });
const ownedBy = (id: string | null) => ({
  assignee: id === null ? null : { ...feature.assignee!, id },
});

const setState: WorkItemAction = {
  ...base,
  kind: 'setState',
  revision: 7,
  stateId: 'state-active',
  stateName: 'Active',
  fromStateId: 'state-new',
  fromStateName: 'New',
};
const assign: WorkItemAction = {
  ...base,
  kind: 'assign',
  revision: 7,
  userId: 'user-priya',
  name: 'Priya Shah',
  fromUserId: 'user-sam',
  fromName: 'Sam Lee',
};
const addNote: WorkItemAction = { ...base, kind: 'addNote', body: 'The S22 numbers are fine' };
const setHealth: WorkItemAction = {
  ...base,
  kind: 'setHealth',
  revision: 7,
  health: 'atRisk',
  from: 'onTrack',
};
const confirmStatus: WorkItemAction = { ...base, kind: 'confirmStatus', revision: 7 };
const setStatus: WorkItemAction = {
  ...base,
  kind: 'setStatus',
  revision: 7,
  text: 'Android download works, iPhone next.',
  from: 'Spike done.',
};

const send = (a: WorkItemAction) => workItemExecutor.send(a, api);
const undo = (a: WorkItemAction) => workItemExecutor.undo!(a, api, null);
const reconcile = (...actions: WorkItemAction[]) =>
  workItemExecutor.reconcile!(
    actions.map((action) => aPending({ action, createdAt: '2030-01-01T09:00:00.000Z' })),
    api,
  );

describe('work item executor', () => {
  it('sends setState as a PATCH of state at the action revision', async () => {
    const fake = world();
    await send(setState);
    const [call] = fake.callsTo(PATCH);
    expect(bodyOf(call).expectedRevision).toBe(7);
    expect(bodyOf(call).changes).toMatchObject({
      state: { value: 'state-active' },
      assignee: null,
    });
    expect(fake.callsTo(PATCH)).toHaveLength(1);
  });

  it('sends assign and clears the owner with null', async () => {
    const fake = world();
    await send(assign);
    await send({ ...assign, userId: null, name: null, revision: 8 });
    const [set, cleared] = fake.callsTo(PATCH).map(bodyOf);
    expect(set.changes).toMatchObject({ assignee: { value: 'user-priya' }, state: null });
    expect(cleared.expectedRevision).toBe(8);
    expect(cleared.changes).toMatchObject({ assignee: { value: null } });
  });

  it('posts a note as the comment add with exactly its body', async () => {
    const fake = world();
    const receipt = (await send(addNote)) as SendReceipt;
    const [call] = fake.callsTo(`POST /api/work-items/${ITEM}/comments`);
    expect(call.body).toEqual({ body: 'The S22 numbers are fine' });
    expect(receipt).toEqual({ createdId: NOTE_IDS[0] });
  });

  it('throws the 409 from the core client unchanged', async () => {
    world();
    const error = await send({ ...setState, revision: 3 }).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiRequestError);
    expect((error as ApiRequestError).status).toBe(409);
  });

  it('undoes a note by archiving the comment it posted', async () => {
    const fake = world();
    const receipt = (await send(addNote)) as SendReceipt;
    await workItemExecutor.undo!(addNote, api, receipt);
    expect(fake.callsTo(`POST /api/comments/${NOTE_IDS[0]}/archive`)).toHaveLength(1);
    // No receipt: nothing is archived, and the undo fails rather than guess.
    await expect(workItemExecutor.undo!(addNote, api, null)).rejects.toThrow();
    expect(fake.callsTo(`POST /api/comments/${NOTE_IDS[0]}/archive`)).toHaveLength(1);
  });

  it('undoes a sent state change only when the state is still the one it set', async () => {
    // The send wrote state-active and moved the item to revision 8.
    const fake = now(8, inState('state-active'));
    await undo(setState);
    const [call] = fake.callsTo(PATCH);
    expect(bodyOf(call).expectedRevision).toBe(8);
    expect(bodyOf(call).changes).toMatchObject({ state: { value: 'state-new' }, assignee: null });

    // Someone moved it on since: nothing is written.
    const moved = now(9, inState('state-resolved'));
    await expect(undo(setState)).rejects.toThrow(/changed since/);
    expect(moved.callsTo(PATCH)).toEqual([]);
  });

  it('undoes an owner and a health change back to what they were', async () => {
    const fake = now(9, { ...ownedBy('user-priya'), health: 'atRisk' });
    await undo(assign);
    await undo(setHealth);
    const [owner, health] = fake.callsTo(PATCH).map(bodyOf);
    expect(owner.expectedRevision).toBe(9);
    expect(owner.changes).toMatchObject({ assignee: { value: 'user-sam' }, state: null });
    expect(health.expectedRevision).toBe(10);
    expect(health.changes).toMatchObject({ health: { value: 'onTrack' }, assignee: null });

    // Clearing the owner is undone by setting the previous one back; the item still has no owner.
    const cleared = now(8, ownedBy(null));
    await undo({ ...assign, userId: null, name: null });
    expect(bodyOf(cleared.callsTo(PATCH)[0]).changes).toMatchObject({
      assignee: { value: 'user-sam' },
    });

    // Someone else took it, or set another health, since: nothing is written.
    const taken = now(9, { ...ownedBy('user-tom'), health: 'blocked' });
    await expect(undo(assign)).rejects.toThrow(/changed since/);
    await expect(undo(setHealth)).rejects.toThrow(/changed since/);
    expect(taken.callsTo(PATCH)).toEqual([]);
  });

  it('undoes a status line by patching the previous line back', async () => {
    // The send was at revision 7; a note and the send moved the item to 9.
    const fake = now(9, { statusLine: 'Android download works, iPhone next.' });
    await undo(setStatus);
    const [call] = fake.callsTo(PATCH);
    expect(bodyOf(call).expectedRevision).toBe(9);
    expect(bodyOf(call).changes).toEqual({ statusLine: { value: 'Spike done.' } });

    // A Feature that had no line has it cleared.
    const first = now(9, { statusLine: 'Android download works, iPhone next.' });
    await undo({ ...setStatus, from: null });
    expect(bodyOf(first.callsTo(PATCH)[0]).changes).toEqual({ statusLine: { value: '' } });

    // A line someone else wrote since is left alone.
    const edited = now(9, { statusLine: 'Someone else wrote this.' });
    await expect(undo(setStatus)).rejects.toThrow(/changed since/);
    expect(edited.callsTo(PATCH)).toEqual([]);
  });

  it('fails an undo on a 409 and does not retry it', async () => {
    const fake = now(8, { health: 'atRisk' });
    fake.failNext(PATCH, { status: 409, body: aConflict({ ...feature, revision: 9 }) });
    await expect(undo(setHealth)).rejects.toMatchObject({ status: 409 });
    expect(fake.callsTo(PATCH)).toHaveLength(1);
  });

  it('reconciles an uncertain setState from the item itself', async () => {
    now(8, inState('state-active'));
    expect(await reconcile(setState)).toEqual(['sent']);
    now(7, inState('state-new'));
    expect(await reconcile(setState)).toEqual(['notSent']);
  });

  it('reconciles an uncertain owner, health and status line from the item itself', async () => {
    now(10, {
      ...ownedBy('user-priya'),
      health: 'atRisk',
      statusLine: 'Android download works, iPhone next.',
    });
    expect(await reconcile(assign, setHealth, setStatus)).toEqual(['sent', 'sent', 'sent']);
    now(7, { ...ownedBy('user-sam'), health: 'onTrack', statusLine: 'Spike done.' });
    expect(await reconcile(assign, setHealth, setStatus)).toEqual([
      'notSent',
      'notSent',
      'notSent',
    ]);
  });

  it('reconciles an uncertain confirmStatus from the verified stamp, not the revision', async () => {
    const stamp = (at: string) => ({
      statusVerified: { at, atLabel: 'just now', stale: false, by: null, agent: null, via: null },
    });
    now(8, stamp('2030-01-01T09:00:30.000Z'));
    expect(await reconcile(confirmStatus)).toEqual(['sent']);
    now(8, stamp('2029-12-20T09:00:00.000Z'));
    expect(await reconcile(confirmStatus)).toEqual(['notSent']);
    now(8, { statusVerified: null });
    expect(await reconcile(confirmStatus)).toEqual(['notSent']);
  });

  it('keeps an action uncertain when its read fails, without failing the rest of the batch', async () => {
    const mine = aMe();
    const fake = workItemApi(
      { 'GET /api/me': mine },
      {
        projectId: PROJECT,
        items: [feature],
        comments: [
          aComment(ITEM, {
            body: addNote.body,
            author: { ...feature.assignee!, id: mine.id },
            createdAt: '2030-01-01T09:00:30.000Z',
          }),
        ],
      },
    );
    fake.failNext(`GET /api/projects/${PROJECT}/work-items/by-key/${KEY}`, {
      status: 500,
      body: { error: 'Boom', code: 'internal_error' },
    });
    const verdicts = await reconcile(setState, addNote);
    // No verdict for the state change (the runner keeps it uncertain); the note is settled.
    expect(0 in verdicts).toBe(false);
    expect(verdicts[1]).toBe('sent');
  });

  it('reconciles approve, reply and discuss from the asks and comments', async () => {
    const mine = aMe();
    const settled = anItemAsks(ITEM, {
      decisions: [
        aDecision({ state: 'confirmed' }),
        aDecision({ number: 2, label: 'D2', state: 'confirmed' }),
      ],
      questions: [aQuestion({ state: 'answered' })],
    });
    const approve: WorkItemAction = { ...base, kind: 'approve', numbers: [1, 2] };
    const reply: WorkItemAction = { ...base, kind: 'reply', number: 1, body: 'Try FunctionGemma' };
    const discuss: WorkItemAction = { ...base, kind: 'discuss', number: 2, body: 'In the dock' };
    workItemApi(
      { 'GET /api/me': mine },
      {
        projectId: PROJECT,
        items: [feature],
        asks: [settled],
        comments: [
          aComment(ITEM, {
            body: 'In the dock',
            author: { ...feature.assignee!, id: mine.id },
            createdAt: '2030-01-01T09:00:30.000Z',
          }),
        ],
      },
    );
    expect(await reconcile(approve, reply, discuss)).toEqual(['sent', 'sent', 'sent']);
    // The default asks: D1 and D2 and Q1 still open, and no comment.
    workItemApi({ 'GET /api/me': mine }, { projectId: PROJECT, items: [feature] });
    expect(await reconcile(approve, reply, discuss)).toEqual(['notSent', 'notSent', 'notSent']);
  });

  it('is reversible for setState, assign, setHealth, setStatus and addNote only', () => {
    const kinds = [
      'setState',
      'assign',
      'addNote',
      'approve',
      'discuss',
      'reply',
      'setHealth',
      'confirmStatus',
      'setStatus',
    ] as const;
    expect(kinds.filter((k) => workItemExecutor.reversible(k))).toEqual([
      'setState',
      'assign',
      'addNote',
      'setHealth',
      'setStatus',
    ]);
  });

  it('reconciles an uncertain note as sent when the comment is there', async () => {
    const mine = aMe();
    const pending = aPending({ action: addNote, createdAt: '2030-01-01T09:00:00.000Z' });
    const posted = aComment(ITEM, {
      body: addNote.body,
      author: { ...feature.assignee!, id: mine.id },
      createdAt: '2030-01-01T09:00:30.000Z',
    });
    const other = aComment(ITEM, {
      body: addNote.body,
      author: { ...posted.author!, id: 'someone-else' },
      createdAt: '2030-01-01T09:00:30.000Z',
    });
    const stale = aComment(ITEM, {
      body: addNote.body,
      author: posted.author,
      createdAt: '2029-12-31T09:00:00.000Z',
    });
    workItemApi(
      { 'GET /api/me': mine },
      { projectId: PROJECT, items: [feature], comments: [posted] },
    );
    expect(await workItemExecutor.reconcile!([pending], api)).toEqual(['sent']);
    workItemApi(
      { 'GET /api/me': mine },
      { projectId: PROJECT, items: [feature], comments: [other, stale] },
    );
    expect(await workItemExecutor.reconcile!([pending], api)).toEqual(['notSent']);
  });

  it('sends approve, discuss and reply through the asks writes', async () => {
    const fake = workItemApi(
      {},
      {
        projectId: PROJECT,
        items: [feature],
        asks: [
          anItemAsks(ITEM, {
            decisions: [aDecision(), aDecision({ number: 2, label: 'D2' })],
            questions: [aQuestion({ number: 3, label: 'Q3' })],
          }),
        ],
      },
    );
    await send({ ...base, kind: 'approve', numbers: [1, 2] });
    await send({ ...base, kind: 'discuss', number: 2, body: 'Put it in the dock' });
    await send({ ...base, kind: 'reply', number: 3, body: 'Try FunctionGemma' });
    expect(fake.callsTo(`POST /api/work-items/${ITEM}/decisions/confirm`)[0].body).toEqual({
      numbers: [1, 2],
    });
    expect(fake.callsTo(`POST /api/work-items/${ITEM}/decisions/2/discuss`)[0].body).toEqual({
      body: 'Put it in the dock',
    });
    expect(fake.callsTo(`POST /api/work-items/${ITEM}/questions/3/answer`)[0].body).toEqual({
      body: 'Try FunctionGemma',
    });
  });

  it('sends setHealth as a PATCH of health and confirmStatus and setStatus through the status writes', async () => {
    const fake = world();
    await send(setHealth);
    await send({ ...confirmStatus, revision: 8 });
    await send({ ...setStatus, revision: 9 });
    const [health, status] = fake.callsTo(PATCH).map(bodyOf);
    expect(health.changes).toMatchObject({ health: { value: 'atRisk' } });
    expect(fake.callsTo(`POST /api/work-items/${ITEM}/status/confirm`)[0].body).toEqual({
      revision: 8,
    });
    expect(status.expectedRevision).toBe(9);
    expect(status.changes).toEqual({
      statusLine: { value: 'Android download works, iPhone next.' },
    });
  });

  it('describes each kind as the preview line in the design', () => {
    const text = (a: WorkItemAction) => workItemExecutor.describe(a).text;
    expect(text(setState)).toBe('State: New → Active');
    expect(text(assign)).toBe('Owner: Sam Lee → Priya Shah');
    expect(text({ ...assign, userId: null, name: null })).toBe('Owner: Sam Lee → No owner');
    expect(text(addNote)).toBe('Note on SHIP-906: "The S22 numbers are fine"');
    expect(text({ ...base, kind: 'approve', numbers: [10] })).toBe('Approve D10');
    expect(text({ ...base, kind: 'discuss', number: 11, body: 'x' })).toBe('Discuss D11');
    const reply = workItemExecutor.describe({ ...base, kind: 'reply', number: 1, body: 'x' });
    expect(reply.text).toBe('Reply to Q1');
    expect(reply.detail).toBe('Posted on SHIP-906. The next agent to pick it up reads it.');
    expect(text(setHealth)).toBe('SHIP-906 health: On track → At risk');
    expect(text(confirmStatus)).toBe('Status still true');
    expect(text(setStatus)).toBe('New status: "Android download works, iPhone next."');
    const lines = [setState, assign, addNote, setHealth, confirmStatus, setStatus].map(
      (a) => workItemExecutor.describe(a).id,
    );
    expect(new Set(lines).size).toBe(lines.length);
  });
});
