import { afterEach, expect, test, vi } from 'vitest';
import fixture from '../../../ShipLean.TestSupport/DtoFixtures/search/search-view.json';
import { fetchSearch, type SearchViewDto } from './search';

// Typecheck fails if the fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
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

afterEach(() => vi.unstubAllGlobals());

test('searchView_ShouldTypeTheFixture', () => {
  const view = typed<SearchViewDto>()(fixture);
  expect(view.projects).toHaveLength(2);
  expect(view.sprints.map((sprint) => sprint.status)).toEqual(['current', 'future']);
  expect(view.items.map((item) => item.matchedIn)).toEqual([
    'title',
    'comment',
    'attachment',
    'description',
  ]);
  expect(view.items[2].attachment).toBe('q3-export.csv');
  expect(view.items[3].parentPath).toHaveLength(3);
  expect(view.more).toBe(true);
});

test('fetchSearch_WhenCalled_ShouldSendTheQueryParameters', async () => {
  const fetchMock = vi.fn<typeof fetch>(
    async () =>
      new Response(JSON.stringify(fixture), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
  );
  vi.stubGlobal('fetch', fetchMock);
  const controller = new AbortController();

  const view = await fetchSearch(
    { q: 'a b&c', prefix: true, closed: true, limit: 8 },
    controller.signal,
  );

  expect(view.items).toHaveLength(4);
  const [path, init] = fetchMock.mock.calls[0];
  expect(path).toBe('/api/search?q=a+b%26c&prefix=true&closed=true&limit=8');
  expect(init?.method).toBe('GET');
  expect(init?.signal).toBe(controller.signal);
});
