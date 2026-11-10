import { afterEach, describe, expect, it, vi } from 'vitest';
import history from '../../../ShipLean.TestSupport/DtoFixtures/workItems/conflict-history.json';
import { ApiRequestError } from './client';
import { confirmStatus, setStatusLine } from './status';
import { conflictOf, type WorkItemDto, type WriteResult } from './workItems';
import { sent, stubFetch } from '../test-support/fetchStub';

afterEach(() => vi.unstubAllGlobals());

const conflict = history.conflict;
const item = conflict.current as WorkItemDto;

describe('status client', () => {
  it('confirmStatus POSTs { revision } and returns the WorkItemDto', async () => {
    const fetchStub = stubFetch(new Response(JSON.stringify(item), { status: 200 }));

    expect(await confirmStatus(item.id, 7)).toEqual(item);

    expect(sent(fetchStub)).toEqual({
      url: `/api/work-items/${item.id}/status/confirm`,
      method: 'POST',
      body: { revision: 7 },
    });
  });

  // C#: WorkItemPatch(ExpectedRevision, Changes) with WorkItemChanges.StatusLine a FieldSet<string>; fields it leaves out
  // (and AddLinks / RemoveLinkIds, which default to []) are left alone.
  it('setStatusLine PATCHes exactly { expectedRevision, changes: { statusLine: { value } } }', async () => {
    const result: WriteResult = {
      id: item.id,
      key: item.key,
      revision: 8,
      changes: [],
      notices: [],
    };
    const fetchStub = stubFetch(new Response(JSON.stringify(result), { status: 200 }));

    expect(await setStatusLine(item.id, 7, 'Card list built; expiry rules next.')).toEqual(result);

    expect(sent(fetchStub)).toEqual({
      url: `/api/work-items/${item.id}`,
      method: 'PATCH',
      body: {
        expectedRevision: 7,
        changes: { statusLine: { value: 'Card list built; expiry rules next.' } },
      },
    });
  });

  it('a 409 rejects with an ApiRequestError conflictOf can read', async () => {
    for (const call of [() => confirmStatus(item.id, 3), () => setStatusLine(item.id, 3, 'x')]) {
      stubFetch(new Response(JSON.stringify(conflict), { status: 409 }));

      const error = await call().catch((e: unknown) => e);

      expect(error).toBeInstanceOf(ApiRequestError);
      expect(conflictOf(error)?.current).toEqual(conflict.current);
    }
  });
});
