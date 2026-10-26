import { afterEach, expect, test, vi } from 'vitest';
import fixture from '../../../ShipLean.TestSupport/DtoFixtures/agents/consent.json';
import {
  decideConsent,
  getConnectedAgents,
  getConsentRequest,
  getMcpUrl,
  revokeConnectedAgent,
  type ConnectedAgentDto,
  type ConsentDecision,
  type ConsentRequestDto,
  type ConsentResult,
  type McpUrlDto,
} from './agents';

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

test('ConsentDto_ShouldRoundTrip', () => {
  const request = typed<ConsentRequestDto>()(fixture.request);
  const unverified = typed<ConsentRequestDto>()(fixture.unverifiedRequest);
  const decision = typed<ConsentDecision>()(fixture.decision);
  const denial = typed<ConsentDecision>()(fixture.denial);
  const result = typed<ConsentResult>()(fixture.result);
  const connected = typed<ConnectedAgentDto[]>()(fixture.connected);
  const mcpUrl = typed<McpUrlDto>()(fixture.mcpUrl);

  expect(request.verified).toBe(true);
  expect(unverified.clientHost).toBeNull();
  expect(decision.allow).toBe(true);
  expect(denial.allow).toBe(false);
  expect(new URL(result.redirectUrl).searchParams.get('iss')).toBe('https://ship.example.test');
  expect(mcpUrl.mcpUrl).toMatch(/\/mcp$/);
  expect(connected.some((agent) => agent.lastUsedAt === null)).toBe(true);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function stubFetch(status: number, body: unknown) {
  const fetchMock = vi.fn(() =>
    Promise.resolve(new Response(body === undefined ? null : JSON.stringify(body), { status })),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function lastCall(fetchMock: ReturnType<typeof stubFetch>) {
  const [path, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
  return { path, method: init.method, body: init.body };
}

test('agents_ShouldCallTheFiveEndpoints', async () => {
  const id = fixture.request.requestId;

  let fetchMock = stubFetch(200, fixture.request);
  expect(await getConsentRequest(id)).toEqual(fixture.request);
  expect(lastCall(fetchMock)).toMatchObject({ path: `/api/agents/consent/${id}`, method: 'GET' });

  fetchMock = stubFetch(200, fixture.result);
  expect(await decideConsent(id, fixture.decision)).toEqual(fixture.result);
  expect(lastCall(fetchMock)).toEqual({
    path: `/api/agents/consent/${id}`,
    method: 'POST',
    body: JSON.stringify(fixture.decision),
  });

  fetchMock = stubFetch(200, fixture.connected);
  expect(await getConnectedAgents()).toEqual(fixture.connected);
  expect(lastCall(fetchMock)).toMatchObject({ path: '/api/me/agents', method: 'GET' });

  fetchMock = stubFetch(204, undefined);
  await revokeConnectedAgent(fixture.connected[0].id);
  expect(lastCall(fetchMock)).toMatchObject({
    path: `/api/me/agents/${fixture.connected[0].id}`,
    method: 'DELETE',
  });

  fetchMock = stubFetch(200, fixture.mcpUrl);
  expect(await getMcpUrl()).toEqual(fixture.mcpUrl);
  expect(lastCall(fetchMock)).toMatchObject({ path: '/api/agents/mcp-url', method: 'GET' });
});
