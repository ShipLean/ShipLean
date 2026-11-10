import { afterEach, describe, expect, it, test, vi } from 'vitest';
import history from '../../../ShipLean.TestSupport/DtoFixtures/asks/history-asks.json';
import feature from '../../../ShipLean.TestSupport/DtoFixtures/asks/item-asks.feature.json';
import story from '../../../ShipLean.TestSupport/DtoFixtures/asks/item-asks.story.json';
import waiting from '../../../ShipLean.TestSupport/DtoFixtures/asks/waiting.json';
import {
  ANSWER_KINDS,
  ASK_EVENT_KINDS,
  DECISION_STATES,
  QUESTION_STATES,
  answerQuestion,
  asksKeys,
  changeDecision,
  confirmDecisions,
  discussDecision,
  getItemAsks,
  getWaiting,
  withdrawDecision,
  withdrawQuestion,
  type ItemAsksDto,
  type QuestionDto,
  type WaitingDto,
} from './asks';
import type { HistoryDto } from './workItems';

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

const sorted = (values: Iterable<string | null>) =>
  [...new Set(values)].filter((v) => v !== null).sort();

test('asks_ShouldTypeEveryFixture', () => {
  const items = [typed<ItemAsksDto>()(story), typed<ItemAsksDto>()(feature)];
  const lines = typed<HistoryDto>()(history);
  const waitingOnMe = typed<WaitingDto>()(waiting);

  expect(items[0].decisions.map((d) => d.label)).toEqual([
    'D1',
    'D2',
    'D3',
    'D4',
    'D5',
    'D6',
    'D7',
  ]);
  expect(items[1].waitingChildren.filter((c) => c.ownerIsCreator)).toHaveLength(1);
  expect(lines.asks.length).toBeGreaterThan(0);
  expect(waitingOnMe.items.length).toBeGreaterThan(0);
});

test('asks_ShouldCoverEveryWireEnumValue', () => {
  const items = [typed<ItemAsksDto>()(story), typed<ItemAsksDto>()(feature)];
  const decisions = items.flatMap((i) => i.decisions);
  const events = decisions.flatMap((d) => d.history);
  const lines = typed<HistoryDto>()(history).asks;

  expect(sorted(items.flatMap((i) => i.questions).map((q) => q.state))).toEqual(
    [...QUESTION_STATES].sort(),
  );
  expect(sorted(decisions.map((d) => d.state))).toEqual([...DECISION_STATES].sort());
  expect(sorted([...decisions.map((d) => d.answerKind), ...events.map((e) => e.toKind)])).toEqual(
    [...ANSWER_KINDS].sort(),
  );
  expect(sorted([...lines.map((l) => l.kind), ...events.map((e) => e.kind)])).toEqual(
    [...ASK_EVENT_KINDS].sort(),
  );
});

function stubFetch(response: Response) {
  const fetchStub = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetchStub);
  return fetchStub;
}

function sent(fetchStub: ReturnType<typeof stubFetch>) {
  const [url, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  return {
    url,
    method: init.method,
    body: init.body === undefined ? undefined : JSON.parse(String(init.body)),
  };
}

const noContent = () => stubFetch(new Response(null, { status: 204 }));

describe('asks client', () => {
  afterEach(() => vi.unstubAllGlobals());
  const itemId = story.workItemId;
  const itemPath = `/api/work-items/${itemId}`;

  it('types the question fixture with options', () => {
    const questions: DeepWiden<QuestionDto>[] = typed<ItemAsksDto>()(story).questions;
    expect(questions.map((q) => q.options)).toEqual([['Yes, until it expires', 'No, hide it'], []]);
    expect(typed<ItemAsksDto>()(feature).questions.map((q) => q.options)).toEqual([[]]);
  });

  it('getItemAsks GETs the item asks and getWaiting GETs what waits on me', async () => {
    let fetchStub = stubFetch(new Response(JSON.stringify(story), { status: 200 }));
    expect(await getItemAsks(itemId)).toEqual(story);
    expect(sent(fetchStub)).toEqual({ url: `${itemPath}/asks`, method: 'GET', body: undefined });

    fetchStub = stubFetch(new Response(JSON.stringify(waiting), { status: 200 }));
    expect(await getWaiting()).toEqual(waiting);
    expect(sent(fetchStub)).toEqual({ url: '/api/me/waiting', method: 'GET', body: undefined });

    expect(asksKeys.item('p1', 'SHIP-141')).toEqual([
      'projects',
      'p1',
      'work-items',
      'SHIP-141',
      'asks',
    ]);
    expect(asksKeys.waiting).toEqual(['me', 'waiting']);
  });

  it('confirmDecisions POSTs exactly { numbers }', async () => {
    const fetchStub = noContent();
    expect(await confirmDecisions(itemId, [1, 3])).toBeUndefined();
    expect(sent(fetchStub)).toEqual({
      url: `${itemPath}/decisions/confirm`,
      method: 'POST',
      body: { numbers: [1, 3] },
    });
  });

  it('changeDecision POSTs exactly the change request', async () => {
    const fetchStub = noContent();
    const request = {
      kind: 'alternative',
      alternativeIndex: 1,
      text: null,
      note: 'Cheaper',
    } as const;
    await changeDecision(itemId, 4, { ...request, extra: 'dropped' } as typeof request);
    expect(sent(fetchStub)).toEqual({
      url: `${itemPath}/decisions/4/change`,
      method: 'POST',
      body: request,
    });
  });

  it('withdrawDecision POSTs with no body', async () => {
    const fetchStub = noContent();
    await withdrawDecision(itemId, 2);
    expect(sent(fetchStub)).toEqual({
      url: `${itemPath}/decisions/2/withdraw`,
      method: 'POST',
      body: undefined,
    });
  });

  it('discussDecision POSTs exactly { body }', async () => {
    const fetchStub = noContent();
    await discussDecision(itemId, 3, 'Why not the profile?');
    expect(sent(fetchStub)).toEqual({
      url: `${itemPath}/decisions/3/discuss`,
      method: 'POST',
      body: { body: 'Why not the profile?' },
    });
  });

  it('answerQuestion POSTs exactly { body }', async () => {
    const fetchStub = noContent();
    await answerQuestion(itemId, 1, '20 MB');
    expect(sent(fetchStub)).toEqual({
      url: `${itemPath}/questions/1/answer`,
      method: 'POST',
      body: { body: '20 MB' },
    });
  });

  it('withdrawQuestion POSTs with no body', async () => {
    const fetchStub = noContent();
    await withdrawQuestion(itemId, 1);
    expect(sent(fetchStub)).toEqual({
      url: `${itemPath}/questions/1/withdraw`,
      method: 'POST',
      body: undefined,
    });
  });
});
