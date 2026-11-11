import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, expect, test, vi } from 'vitest';

type Handler = (event: { sprintIds: string[]; workItemIds: string[] }) => void;
const handlers: Handler[] = [];
vi.mock('../../realtime', () => ({
  registerInvalidation: (_type: string, handler: Handler) => {
    handlers.push(handler);
    return () => {};
  },
}));
// A refetch that never ends keeps the query marked invalidated, so the mark can be read.
vi.mock('@shiplean/core/api/retrosApi', () => {
  const pending = () => new Promise(() => {});
  return {
    fetchSprintRetro: pending,
    fetchRetroSearch: pending,
    fetchRetroInsights: pending,
    fetchRetroThemes: pending,
    fetchWorkItemRetros: pending,
    fetchRetroFeatures: pending,
    fetchRetroItem: pending,
    fetchRetroActionDialog: pending,
    fetchRetroAction: pending,
  };
});

const queries = await import('./useRetroQueries');

const keys = {
  sprint: queries.sprintRetroKey('p1', 's24'),
  otherSprint: queries.sprintRetroKey('p1', 's23'),
  item: queries.workItemRetrosKey('p1', 'w1'),
  search: queries.retroSearchKey('p1', {}),
  insights: queries.retroInsightsKey('p1', '6'),
  themes: queries.retroThemesKey('p1'),
  features: queries.retroFeaturesKey('p1'),
  retroItem: queries.retroItemKey('p1', 'SHIP-1'),
  dialog: queries.retroActionDialogKey('p1', { pointIds: ['a'] }),
  action: queries.retroActionKey('p1', 'w2'),
};

function Hooks() {
  queries.useSprintRetro('p1', 's24');
  queries.useWorkItemRetros('p1', 'w1');
  queries.useRetroSearch('p1', {});
  queries.useRetroInsights('p1', '6');
  queries.useRetroThemes('p1');
  queries.useRetroFeatures('p1');
  queries.useRetroItem('p1', 'SHIP-1');
  queries.useRetroActionDialogData('p1', { pointIds: ['a'] });
  queries.useRetroAction('p1', 'w2');
  return null;
}

let client: QueryClient;

beforeEach(() => {
  handlers.length = 0;
  client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } });
  for (const key of Object.values(keys)) client.setQueryData(key, {});
  act(() =>
    createRoot(document.createElement('div')).render(
      <QueryClientProvider client={client}>
        <Hooks />
      </QueryClientProvider>,
    ),
  );
});

const marked = () =>
  Object.entries(keys)
    .filter(([, key]) => client.getQueryState(key)?.isInvalidated)
    .map(([name]) => name);

const deliver = (sprintIds: string[], workItemIds: string[]) =>
  act(() => handlers.forEach((handler) => handler({ sprintIds, workItemIds })));

test('RetroChanged_WhenEventIsForAnotherSprintAndItem_ShouldMarkOnlyTheProjectWideReads', () => {
  deliver(['s23'], ['w-other']);
  expect(marked()).toEqual([
    'search',
    'insights',
    'themes',
    'features',
    'retroItem',
    'dialog',
    'action',
  ]);
});

test('RetroChanged_WhenEventIsForThisSprintAndItem_ShouldMarkThemAndTheProjectReads', () => {
  deliver(['s24'], ['w1']);
  expect(marked()).toEqual([
    'sprint',
    'item',
    'search',
    'insights',
    'themes',
    'features',
    'retroItem',
    'dialog',
    'action',
  ]);
});

test('RetroChanged_WhenListsAreEmpty_ShouldMarkEveryOpenRetroRead', () => {
  deliver([], []);
  // The other sprint's query has no screen open, so nothing refetches it; the event must not reach it through a prefix.
  expect(marked()).toEqual([
    'sprint',
    'item',
    'search',
    'insights',
    'themes',
    'features',
    'retroItem',
    'dialog',
    'action',
  ]);
});

test('NewHooks_ShouldKeepEveryKeyUnderTheRetrosRootKey', () => {
  const root = queries.retrosRootKey('p1');
  for (const key of Object.values(keys)) expect(key.slice(0, root.length)).toEqual([...root]);
  expect(queries.retroFeaturesKey('p1')).toEqual([...root, 'features']);
  expect(queries.retroItemKey('p1', 'SHIP-1')).toEqual([...root, 'item', 'SHIP-1']);
});
