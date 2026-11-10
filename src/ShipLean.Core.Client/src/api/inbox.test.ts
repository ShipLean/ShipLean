import { afterEach, describe, expect, it, vi } from 'vitest';
import answered from '../../../ShipLean.TestSupport/DtoFixtures/inbox/answered.json';
import inbox from '../../../ShipLean.TestSupport/DtoFixtures/inbox/inbox.json';
import reply from '../../../ShipLean.TestSupport/DtoFixtures/inbox/mention-reply.json';
import {
  ANSWERED_NEXT_KINDS,
  INBOX_ASK_KINDS,
  getAnswered,
  getInbox,
  inboxKeys,
  markMentionSeen,
  replyToMention,
  type AnsweredDto,
  type InboxDto,
  type MentionReplyRequest,
} from './inbox';

// Typecheck fails if a fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
type DeepWiden<T> = T extends string
  ? string
  : T extends number
    ? number
    : T extends boolean
      ? boolean
      : T extends readonly (infer U)[]
        ? DeepWiden<U>[]
        : T extends object
          ? { [K in keyof T]: DeepWiden<T[K]> }
          : T;

type NoExtraKeys<U, T> = U extends readonly (infer UE)[]
  ? T extends readonly (infer TE)[]
    ? NoExtraKeys<UE, TE>[]
    : U
  : U extends object
    ? T extends object
      ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], T[K]> : never }
      : U
    : U;

function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

function stubFetch(response: () => Response) {
  const fetchStub = vi.fn().mockImplementation(() => Promise.resolve(response()));
  vi.stubGlobal('fetch', fetchStub);
  return fetchStub;
}

function sent(fetchStub: ReturnType<typeof stubFetch>, call = 0) {
  const [url, init] = fetchStub.mock.calls[call] as [string, RequestInit];
  return {
    url,
    method: init.method,
    body: init.body === undefined ? undefined : JSON.parse(String(init.body)),
  };
}

afterEach(() => vi.unstubAllGlobals());

describe('inbox fixtures', () => {
  it('types inbox.json as InboxDto and answered.json as AnsweredDto with no extra keys', () => {
    const waiting = typed<InboxDto>()(inbox);
    const done = typed<AnsweredDto>()(answered);
    typed<MentionReplyRequest>()(reply);

    const asks = waiting.items.flatMap((i) => i.asks);
    expect([...new Set(asks.map((a) => a.kind))].sort()).toEqual([...INBOX_ASK_KINDS].sort());
    expect(
      [...new Set(done.rows.flatMap((r) => (r.next === null ? [] : [r.next.kind])))].sort(),
    ).toEqual([...ANSWERED_NEXT_KINDS].sort());
    expect(done.rows.some((r) => r.next === null)).toBe(true);
  });
});

describe('inbox client', () => {
  const projectId = inbox.projectId;

  it('getInbox and getAnswered GET their routes', async () => {
    const inboxFetch = stubFetch(() => new Response(JSON.stringify(inbox), { status: 200 }));
    expect(await getInbox(projectId)).toEqual(inbox);
    expect(sent(inboxFetch)).toEqual({
      url: `/api/projects/${projectId}/inbox`,
      method: 'GET',
      body: undefined,
    });

    const answeredFetch = stubFetch(() => new Response(JSON.stringify(answered), { status: 200 }));
    expect(await getAnswered(projectId)).toEqual(answered);
    expect(sent(answeredFetch)).toEqual({
      url: `/api/projects/${projectId}/inbox/answered`,
      method: 'GET',
      body: undefined,
    });

    expect(inboxKeys.inbox(projectId)).toEqual(['inbox', projectId]);
    expect(inboxKeys.answered(projectId)).toEqual(['inbox', projectId, 'answered']);
  });

  it('markMentionSeen POSTs with no body', async () => {
    const fetchStub = stubFetch(() => new Response(null, { status: 204 }));
    const mentionId = inbox.items[1].asks[1].mention!.mentionId;

    expect(await markMentionSeen(mentionId)).toBeUndefined();

    expect(sent(fetchStub)).toEqual({
      url: `/api/mentions/${mentionId}/seen`,
      method: 'POST',
      body: undefined,
    });
  });

  it('replyToMention POSTs exactly { body }', async () => {
    const fetchStub = stubFetch(() => new Response(null, { status: 204 }));
    const mentionId = inbox.items[1].asks[2].mention!.mentionId;

    expect(await replyToMention(mentionId, reply.body)).toBeUndefined();

    expect(sent(fetchStub)).toEqual({
      url: `/api/mentions/${mentionId}/reply`,
      method: 'POST',
      body: reply,
    });
  });
});
