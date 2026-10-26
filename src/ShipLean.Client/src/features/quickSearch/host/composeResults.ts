import type { QuickSearchResults } from '../types';

// With nothing typed the dialog shows Recent then Actions; otherwise the search results then Actions, with the status,
// note and retry of the search.
export function composeResults(
  query: string,
  search: QuickSearchResults,
  recent: QuickSearchResults,
  actions: QuickSearchResults,
): QuickSearchResults {
  if (query.trim() === '') {
    return {
      groups: [...recent.groups, ...actions.groups],
      status: 'ready',
      note: recent.note,
      retry: null,
    };
  }
  return {
    groups: [...search.groups, ...actions.groups],
    status: search.status,
    note: search.note,
    retry: search.retry,
  };
}
