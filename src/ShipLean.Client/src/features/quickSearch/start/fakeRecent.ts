import type { QuickSearchRow } from '../types';

// TODO: replaced by the items opened in this browser, in useRecentSource.
export const fakeRecentRows: readonly QuickSearchRow[] = [
  {
    kind: 'item',
    id: 'SHIP-41',
    item: {
      key: 'SHIP-41',
      title: 'Proxy sign-in for reverse-proxy installs',
      type: 'feature',
      state: 'Active',
      stateCategory: 'inProgress',
      parentPath: ['SHIP-12'],
      projectKey: 'SHIP',
    },
    match: null,
  },
  {
    kind: 'item',
    id: 'SHIP-97',
    item: {
      key: 'SHIP-97',
      title: 'Sprint review export',
      type: 'story',
      state: 'Active',
      stateCategory: 'inProgress',
      parentPath: ['SHIP-30'],
      projectKey: 'SHIP',
    },
    match: null,
  },
];
