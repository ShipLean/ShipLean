import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  addItems,
  backlogKeys,
  bulkPlace,
  getBacklog,
  reorder,
  type BacklogFilter,
} from './backlog';
import type { BacklogAddItem, BulkWriteItem } from './backlogWrites';

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

const filter: BacklogFilter = { mode: 'list', filter: '{"a":1}', includeCompleted: false };
const item: BacklogAddItem = {
  type: 'story',
  title: 't',
  description: null,
  points: null,
  parentId: null,
  tasks: [],
};

describe('backlog', () => {
  it('getBacklog uses /backlog for stories and /backlogs/{level} for features and epics', async () => {
    const f = encodeURIComponent('{"a":1}');
    const stories = stubFetch(ok());
    await getBacklog('p/1', 'stories', filter);
    expect(sent(stories)).toEqual({
      url: `/api/projects/p%2F1/backlog?mode=list&filter=${f}&includeCompleted=false`,
      method: 'GET',
      body: undefined,
    });
    const features = stubFetch(ok());
    await getBacklog('p/1', 'features', filter);
    expect(sent(features).url).toBe(
      `/api/projects/p%2F1/backlogs/features?filter=${f}&includeCompleted=false`,
    );
    const epics = stubFetch(ok());
    await getBacklog('p', 'epics', { ...filter, mode: 'tree', includeCompleted: true });
    expect(sent(epics).url).toBe(
      `/api/projects/p/backlogs/epics?mode=tree&filter=${f}&includeCompleted=true`,
    );
  });

  it('backlogKeys.level differs by level and filter', () => {
    expect(backlogKeys.level('p', 'stories', filter)).not.toEqual(
      backlogKeys.level('p', 'features', filter),
    );
    expect(backlogKeys.level('p', 'stories', filter)).not.toEqual(
      backlogKeys.level('p', 'stories', { ...filter, includeCompleted: true }),
    );
  });

  it('reorder POSTs the reorder route with { expectedRevision, beforeItemId }', async () => {
    const stub = stubFetch(ok());
    await reorder('a/b', 3, null);
    expect(sent(stub)).toEqual({
      url: '/api/work-items/a%2Fb/reorder',
      method: 'POST',
      body: { expectedRevision: 3, beforeItemId: null },
    });
  });

  it('bulkPlace POSTs the bulk route with { items }', async () => {
    const stub = stubFetch(ok());
    const items: BulkWriteItem[] = [
      {
        id: 'i',
        expectedRevision: 1,
        changes: null,
        placement: { slot: 'first', beforeItemId: null },
      },
    ];
    await bulkPlace('p/1', items);
    expect(sent(stub)).toEqual({
      url: '/api/projects/p%2F1/backlog/bulk',
      method: 'POST',
      body: { items },
    });
  });

  it('addItems POSTs { items, sprintId, placement }', async () => {
    const stub = stubFetch(ok());
    const placement = { slot: 'before', beforeItemId: 'x' } as const;
    await addItems('p/1', [item], placement, 's1');
    expect(sent(stub)).toEqual({
      url: '/api/projects/p%2F1/backlog/items',
      method: 'POST',
      body: { items: [item], sprintId: 's1', placement },
    });
  });

  it('addItems sends a null placement as null, which the server reads as first', async () => {
    const stub = stubFetch(ok());
    await addItems('p', [item], null);
    expect(sent(stub).body).toEqual({ items: [item], sprintId: null, placement: null });
  });
});
