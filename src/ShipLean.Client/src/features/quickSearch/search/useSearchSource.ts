import type { QuickSearchResults } from '../types';
import { fakeSearchGroups } from './fakeSearch';

// TODO: replace with GET /api/search: debounced, with late answers dropped, the current project's items first.
export function useSearchSource(query: string): QuickSearchResults {
  return {
    groups: query.trim() === '' ? [] : fakeSearchGroups,
    status: 'ready',
    note: null,
    retry: null,
  };
}
