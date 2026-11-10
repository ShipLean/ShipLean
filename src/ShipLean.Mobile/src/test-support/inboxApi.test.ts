import {
  answerQuestion,
  changeDecision,
  confirmDecisions,
  discussDecision,
  withdrawDecision,
  withdrawQuestion,
} from '@shiplean/core/api/asks';
import { ApiRequestError } from '@shiplean/core/api/client';
import { getAnswered, getInbox, markMentionSeen, replyToMention } from '@shiplean/core/api/inbox';
import { confirmStatus, setStatusLine } from '@shiplean/core/api/status';
import { afterEach, describe, expect, it } from 'vitest';
import { restoreFetch } from './fakeApi';
import {
  aDecisionAsk,
  aMentionAsk,
  anAnswered,
  anInbox,
  anInboxItem,
  aQuestionAsk,
  aStatusAsk,
  inboxApi,
} from './inbox';

afterEach(restoreFetch);

function everyKind() {
  const decided = anInboxItem({ asks: [aQuestionAsk({ number: 2 }), aDecisionAsk({ number: 3 })] });
  const mentioned = anInboxItem({ asks: [aMentionAsk()] });
  const status = anInboxItem({ revision: 7, asks: [aStatusAsk()] });
  return { inbox: anInbox({ items: [decided, mentioned, status] }), decided, mentioned, status };
}

const settle = async () => {
  for (let i = 0; i < 20; i++) await Promise.resolve();
};

describe('inboxApi', () => {
  it('answers each write in its final shape', async () => {
    const { inbox, decided, mentioned, status } = everyKind();
    const answered = anAnswered();
    const fake = inboxApi({}, { inbox, answered });

    expect(await getInbox(inbox.projectId)).toEqual(inbox);
    expect(await getAnswered(inbox.projectId)).toEqual(answered);

    await expect(confirmDecisions(decided.id, [3])).resolves.toBeUndefined();
    await expect(
      changeDecision(decided.id, 3, {
        kind: 'alternative',
        alternativeIndex: 0,
        text: null,
        note: null,
      }),
    ).resolves.toBeUndefined();
    await expect(withdrawDecision(decided.id, 3)).resolves.toBeUndefined();
    await expect(discussDecision(decided.id, 3, 'Why?')).resolves.toBeUndefined();
    await expect(answerQuestion(decided.id, 2, 'Yes.')).resolves.toBeUndefined();
    await expect(withdrawQuestion(decided.id, 2)).resolves.toBeUndefined();

    const mentionId = mentioned.asks[0].mention?.mentionId ?? '';
    await expect(markMentionSeen(mentionId)).resolves.toBeUndefined();
    await expect(replyToMention(mentionId, 'On it.')).resolves.toBeUndefined();

    const confirmed = await confirmStatus(status.id, status.revision);
    expect(confirmed).toMatchObject({ id: status.id, key: status.key, revision: 8 });
    expect(confirmed.statusLine).toBe(status.asks[0].status?.statusLine);
    const write = await setStatusLine(status.id, status.revision, 'Shipped.');
    expect(write).toMatchObject({ id: status.id, key: status.key, revision: 8, changes: [] });

    const writes = fake.calls.filter((c) => c.method !== 'GET');
    expect(writes).toHaveLength(10);
    expect(fake.callsTo(`POST /api/mentions/${mentionId}/reply`)[0].body).toEqual({
      body: 'On it.',
    });
  });

  it('fails the next call to a route once with failNext', async () => {
    const { inbox, decided } = everyKind();
    const fake = inboxApi({}, { inbox });
    const route = `POST /api/work-items/${decided.id}/decisions/confirm`;
    fake.failNext(route, { status: 500, body: { error: 'Boom', code: 'internal_error' } });
    await expect(confirmDecisions(decided.id, [3])).rejects.toBeInstanceOf(ApiRequestError);
    await expect(confirmDecisions(decided.id, [3])).resolves.toBeUndefined();
    expect(fake.callsTo(route)).toHaveLength(2);
  });

  it('rejects every call while offline', async () => {
    const { inbox, decided } = everyKind();
    const fake = inboxApi({}, { inbox });
    fake.offline(true);
    await expect(getInbox(inbox.projectId)).rejects.toBeInstanceOf(TypeError);
    await expect(answerQuestion(decided.id, 2, 'Yes.')).rejects.toBeInstanceOf(TypeError);
    fake.offline(false);
    await expect(getInbox(inbox.projectId)).resolves.toEqual(inbox);
  });

  it('never settles a hung route', async () => {
    const { inbox, mentioned } = everyKind();
    const fake = inboxApi({}, { inbox });
    const mentionId = mentioned.asks[0].mention?.mentionId ?? '';
    const route = `POST /api/mentions/${mentionId}/seen`;
    fake.hang(route);
    let settled = false;
    const sent = markMentionSeen(mentionId).finally(() => {
      settled = true;
    });
    await settle();
    expect(settled).toBe(false);
    fake.release(route);
    await sent;
    expect(settled).toBe(true);
  });

  it('answers an unlisted route 404', async () => {
    const { inbox, decided } = everyKind();
    inboxApi({}, { inbox });
    // Q9 isn't one of the item's asks.
    const unlisted = answerQuestion(decided.id, 9, 'Yes.');
    await expect(unlisted).rejects.toBeInstanceOf(ApiRequestError);
    await expect(unlisted).rejects.toMatchObject({ status: 404, code: 'not_found' });
    await expect(getInbox('another-project')).rejects.toBeInstanceOf(ApiRequestError);
  });
});
