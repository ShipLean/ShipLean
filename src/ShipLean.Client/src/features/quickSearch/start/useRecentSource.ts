import type { QuickSearchResults } from '../types';
import { fakeRecentRows } from './fakeRecent';

// TODO: replace with the items opened in this browser, per signed-in user.
export function useRecentSource(): QuickSearchResults {
  return {
    groups: [{ id: 'recent', title: 'Recent', rows: fakeRecentRows }],
    status: 'ready',
    note: null,
    retry: null,
  };
}
