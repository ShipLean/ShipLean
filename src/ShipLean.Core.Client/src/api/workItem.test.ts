import { afterEach, describe, expect, it, vi } from 'vitest';
import conflictHistory from '../../../ShipLean.TestSupport/DtoFixtures/workItems/conflict-history.json';
import { asksKeys } from './asks';
import { ApiRequestError } from './client';
import { conflictOf, type WorkItemChanges } from './workItems';
import { getChildren, getItemByKey, patchItem, workItemKeys } from './workItem';

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
    body: init.body === undefined ? undefined : (JSON.parse(String(init.body)) as unknown),
  };
}

const ok = (body: unknown = {}, status = 200) => new Response(JSON.stringify(body), { status });

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('workItem', () => {
  it('getItemByKey GETs the by-key route with both ids encoded', async () => {
    const stub = stubFetch(ok());
    await getItemByKey('p/1', 'SHIP 1');
    expect(sent(stub)).toEqual({
      url: '/api/projects/p%2F1/work-items/by-key/SHIP%201',
      method: 'GET',
      body: undefined,
    });
  });

  it('patchItem PATCHes the item with { expectedRevision, changes }', async () => {
    const stub = stubFetch(ok());
    const changes = { state: { value: 's1' } } as WorkItemChanges;
    await patchItem('a/b', 4, changes);
    expect(sent(stub)).toEqual({
      url: '/api/work-items/a%2Fb',
      method: 'PATCH',
      body: { expectedRevision: 4, changes },
    });
  });

  it('patchItem sends { expectedRevision, changes } and a 409 rejects with a ConflictDto conflictOf reads', async () => {
    stubFetch(ok(conflictHistory.conflict, 409));
    const error: unknown = await patchItem('id', 1, {} as WorkItemChanges).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiRequestError);
    expect(conflictOf(error)).toEqual(conflictHistory.conflict);
  });

  it('getChildren GETs the children route and adds the type only when given', async () => {
    const stub = stubFetch(ok([]));
    await getChildren('i/d');
    expect(sent(stub)).toEqual({
      url: '/api/work-items/i%2Fd/children',
      method: 'GET',
      body: undefined,
    });
    const typed = stubFetch(ok([]));
    await getChildren('id', 'story');
    expect(sent(typed).url).toBe('/api/work-items/id/children?type=story');
  });

  it('workItemKeys are under the project like the SPA and the asks key', () => {
    expect(workItemKeys.byKey('p', 'K-1')).toEqual(['projects', 'p', 'work-items', 'K-1']);
    expect(workItemKeys.children('i')).toEqual(['workItem', 'children', 'i']);
    expect(workItemKeys.byKey('p', 'K').slice(0, 3)).toEqual(workItemKeys.item('p'));
    expect(asksKeys.item('p', 'K').slice(0, 4)).toEqual(workItemKeys.byKey('p', 'K'));
  });
});
