import { ApiRequestError } from '@shiplean/core/api/client';
import { afterEach, describe, expect, it } from 'vitest';
import type { Api } from '../actions/types';
import { aConflict, aWorkItem } from '../test-support/builders';
import { restoreFetch } from '../test-support/fakeApi';
import {
  aDecisionAsk,
  aMentionAsk,
  anInbox,
  anInboxItem,
  aQuestionAsk,
  aStatusAsk,
  inboxApi,
} from '../test-support/inbox';
import type { InboxAction, InboxKind } from './actions';
import { inboxExecutor } from './executor';
import { toBatch } from './reviewBatch';

afterEach(restoreFetch);

const KINDS: InboxKind[] = [
  'approve',
  'chooseDecision',
  'chooseOption',
  'answer',
  'discuss',
  'review',
  'mentionReply',
  'seen',
  'stillTrue',
  'newStatus',
];

// The executor sends through @shiplean/core, which reads the installed fake at fetch; the api argument is unused.
const api = {} as Api;

function world() {
  const decided = anInboxItem({
    asks: [
      aDecisionAsk({ number: 1 }),
      aDecisionAsk({ number: 2 }),
      aDecisionAsk({ number: 3, alternatives: ['A', 'B'] }),
      aQuestionAsk({ number: 2, options: ['10 MB', '50 MB'] }),
    ],
  });
  const mention = aMentionAsk();
  const mentioned = anInboxItem({ asks: [mention] });
  const status = anInboxItem({
    revision: 7,
    asks: [aStatusAsk({ statusLine: 'Card list built.' })],
  });
  const inbox = anInbox({ items: [decided, mentioned, status] });
  const on = (item: typeof decided) =>
    ({ area: 'inbox', projectId: inbox.projectId, itemId: item.id, key: item.key }) as const;
  const mentionId = mention.mention!.mentionId;
  const byKey = `GET /api/projects/${inbox.projectId}/work-items/by-key/${status.key}`;
  const fake = inboxApi(
    {
      [byKey]: aWorkItem({
        id: status.id,
        key: status.key,
        revision: 8,
        statusLine: 'Cards saved.',
      }),
    },
    { inbox },
  );
  return { fake, inbox, decided, status, on, mentionId, byKey };
}

const send = (a: InboxAction) => inboxExecutor.send(a, api);
const bodies = (fake: ReturnType<typeof world>['fake'], route: string) =>
  fake.callsTo(route).map((c) => c.body);

describe('inbox executor', () => {
  it('marks only newStatus reversible', () => {
    expect(inboxExecutor.area).toBe('inbox');
    expect(KINDS.filter((k) => inboxExecutor.reversible(k))).toEqual(['newStatus']);
  });

  it('sends approve as confirmDecisions', async () => {
    const { fake, decided, on } = world();
    await send({ ...on(decided), kind: 'approve', numbers: [1, 2] });
    expect(bodies(fake, `POST /api/work-items/${decided.id}/decisions/confirm`)).toEqual([
      { numbers: [1, 2] },
    ]);
  });

  it('sends chooseDecision with no index as confirmDecisions of its number', async () => {
    const { fake, decided, on } = world();
    await send({ ...on(decided), kind: 'chooseDecision', number: 3, alternativeIndex: null });
    expect(bodies(fake, `POST /api/work-items/${decided.id}/decisions/confirm`)).toEqual([
      { numbers: [3] },
    ]);
    expect(fake.callsTo(`POST /api/work-items/${decided.id}/decisions/3/change`)).toEqual([]);
  });

  it('sends chooseDecision with an index as changeDecision to that alternative', async () => {
    const { fake, decided, on } = world();
    await send({ ...on(decided), kind: 'chooseDecision', number: 3, alternativeIndex: 1 });
    expect(bodies(fake, `POST /api/work-items/${decided.id}/decisions/3/change`)).toEqual([
      { kind: 'alternative', alternativeIndex: 1, text: null, note: null },
    ]);
  });

  it('sends chooseOption as answerQuestion with the option text', async () => {
    const { fake, decided, on } = world();
    await send({
      ...on(decided),
      kind: 'chooseOption',
      number: 2,
      optionIndex: 1,
      option: '50 MB',
    });
    expect(bodies(fake, `POST /api/work-items/${decided.id}/questions/2/answer`)).toEqual([
      { body: '50 MB' },
    ]);
  });

  it('sends answer as answerQuestion', async () => {
    const { fake, decided, on } = world();
    await send({ ...on(decided), kind: 'answer', number: 2, body: 'Fifty, and trim the rest' });
    expect(bodies(fake, `POST /api/work-items/${decided.id}/questions/2/answer`)).toEqual([
      { body: 'Fifty, and trim the rest' },
    ]);
  });

  it('sends discuss as discussDecision', async () => {
    const { fake, decided, on } = world();
    await send({ ...on(decided), kind: 'discuss', number: 1, body: 'Why not the profile?' });
    expect(bodies(fake, `POST /api/work-items/${decided.id}/decisions/1/discuss`)).toEqual([
      { body: 'Why not the profile?' },
    ]);
  });

  it('sends review as its confirm then one changeDecision per change', async () => {
    const { fake, decided, on } = world();
    const review: InboxAction = {
      ...on(decided),
      kind: 'review',
      confirm: [1],
      changes: [
        { number: 2, alternativeIndex: null, ownText: 'Keep it in the profile', note: 'Cheaper' },
        { number: 3, alternativeIndex: 0, ownText: null, note: null },
      ],
    };
    // The Inbox sends a review as its batch; each part is one send.
    for (const part of toBatch(review)) await send(part);
    const path = `/api/work-items/${decided.id}/decisions`;
    expect(fake.calls.map((c) => `${c.method} ${c.path}`)).toEqual([
      `POST ${path}/confirm`,
      `POST ${path}/2/change`,
      `POST ${path}/3/change`,
    ]);
    expect(bodies(fake, `POST ${path}/confirm`)).toEqual([{ numbers: [1] }]);
    expect(bodies(fake, `POST ${path}/2/change`)).toEqual([
      { kind: 'own', alternativeIndex: null, text: 'Keep it in the profile', note: 'Cheaper' },
    ]);
    expect(bodies(fake, `POST ${path}/3/change`)).toEqual([
      { kind: 'alternative', alternativeIndex: 0, text: null, note: null },
    ]);
  });

  it('sends mentionReply as replyToMention', async () => {
    const { fake, inbox, on, mentionId } = world();
    await send({
      ...on(inbox.items[1]!),
      kind: 'mentionReply',
      mentionId,
      label: '@1',
      body: 'Done',
    });
    expect(bodies(fake, `POST /api/mentions/${mentionId}/reply`)).toEqual([{ body: 'Done' }]);
  });

  it('sends seen as markMentionSeen', async () => {
    const { fake, inbox, on, mentionId } = world();
    await send({ ...on(inbox.items[1]!), kind: 'seen', mentionId, label: '@1' });
    expect(fake.callsTo(`POST /api/mentions/${mentionId}/seen`)).toHaveLength(1);
  });

  it('sends stillTrue as confirmStatus at its revision', async () => {
    const { fake, status, on } = world();
    await expect(send({ ...on(status), kind: 'stillTrue', revision: 7 })).resolves.toBeUndefined();
    expect(bodies(fake, `POST /api/work-items/${status.id}/status/confirm`)).toEqual([
      { revision: 7 },
    ]);
  });

  it('sends newStatus as setStatusLine at its revision', async () => {
    const { fake, status, on } = world();
    await expect(
      send({
        ...on(status),
        kind: 'newStatus',
        revision: 7,
        text: 'Cards saved.',
        from: 'Card list built.',
      }),
    ).resolves.toBeUndefined();
    expect(bodies(fake, `PATCH /api/work-items/${status.id}`)).toEqual([
      { expectedRevision: 7, changes: { statusLine: { value: 'Cards saved.' } } },
    ]);
  });

  it('describes approve of D10 as "D10 approved"', () => {
    const { decided, on } = world();
    expect(inboxExecutor.describe({ ...on(decided), kind: 'approve', numbers: [10] }).text).toBe(
      'D10 approved',
    );
  });

  it('describes approve of D1 to D8 as "D1–D8 confirmed"', () => {
    const { decided, on } = world();
    const numbers = [1, 2, 3, 4, 5, 6, 7, 8];
    expect(inboxExecutor.describe({ ...on(decided), kind: 'approve', numbers }).text).toBe(
      'D1–D8 confirmed',
    );
  });

  it('describes every other kind as the design words it', () => {
    const { decided, on, mentionId } = world();
    const at = on(decided);
    const text = (a: InboxAction) => inboxExecutor.describe(a).text;
    expect(text({ ...at, kind: 'chooseDecision', number: 3, alternativeIndex: null })).toBe(
      'D3 approved',
    );
    expect(text({ ...at, kind: 'chooseDecision', number: 3, alternativeIndex: 1 })).toBe(
      'D3 changed',
    );
    expect(text({ ...at, kind: 'review', confirm: [1, 2], changes: [] })).toBe('D1–D2 confirmed');
    expect(
      text({
        ...at,
        kind: 'review',
        confirm: [1],
        changes: [{ number: 3, alternativeIndex: 0, ownText: null, note: null }],
      }),
    ).toBe('D1, D3: 1 recommended, D3 changed');
    expect(text({ ...at, kind: 'chooseOption', number: 1, optionIndex: 0, option: 'x' })).toBe(
      'Answered Q1',
    );
    expect(text({ ...at, kind: 'answer', number: 2, body: 'x' })).toBe('Answered Q2');
    expect(text({ ...at, kind: 'discuss', number: 4, body: 'x' })).toBe('Discussed D4');
    expect(text({ ...at, kind: 'mentionReply', mentionId, label: '@1', body: 'x' })).toBe(
      'Replied to @1',
    );
    expect(text({ ...at, kind: 'seen', mentionId, label: '@1' })).toBe('@1 seen');
    expect(text({ ...at, kind: 'stillTrue', revision: 1 })).toBe('Status still true');
    expect(text({ ...at, kind: 'newStatus', revision: 1, text: 'x', from: 'y' })).toBe(
      'New status',
    );
  });

  it('is reversible only for newStatus, whose undo patches the previous line back', async () => {
    const { fake, status, on, byKey } = world();
    expect(KINDS.filter((k) => inboxExecutor.reversible(k))).toEqual(['newStatus']);
    const newStatus: InboxAction = {
      ...on(status),
      kind: 'newStatus',
      revision: 7,
      text: 'Cards saved.',
      from: 'Card list built.',
    };

    await inboxExecutor.undo!(newStatus, api, null);

    expect(fake.callsTo(byKey)).toHaveLength(1);
    // At the item's current revision (8, after the send), not the action's 7.
    expect(bodies(fake, `PATCH /api/work-items/${status.id}`)).toEqual([
      { expectedRevision: 8, changes: { statusLine: { value: 'Card list built.' } } },
    ]);
  });

  it('fails the undo without writing when the line changed since the send', async () => {
    const { fake, status, on } = world();
    const undo = inboxExecutor.undo!(
      {
        ...on(status),
        kind: 'newStatus',
        revision: 7,
        text: 'Someone else wrote this.',
        from: 'Old.',
      },
      api,
      null,
    );
    await expect(undo).rejects.toThrow();
    expect(fake.callsTo(`PATCH /api/work-items/${status.id}`)).toEqual([]);
  });

  it('fails the undo on a 409 from the PATCH', async () => {
    const { fake, status, on } = world();
    const route = `PATCH /api/work-items/${status.id}`;
    fake.failNext(route, { status: 409, body: aConflict(aWorkItem({ id: status.id })) });
    const undo = inboxExecutor.undo!(
      {
        ...on(status),
        kind: 'newStatus',
        revision: 7,
        text: 'Cards saved.',
        from: 'Card list built.',
      },
      api,
      null,
    );
    await expect(undo).rejects.toBeInstanceOf(ApiRequestError);
    expect(fake.callsTo(route)).toHaveLength(1);
  });
});
