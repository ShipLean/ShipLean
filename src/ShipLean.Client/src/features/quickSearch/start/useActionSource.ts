import type { QuickSearchResults } from '../types';
import { fakeActionRows } from './fakeActions';

// TODO: replace with the real actions (create, go to, toggle theme).
export function useActionSource(query: string): QuickSearchResults {
  const needle = query.trim().toLowerCase();
  const rows = fakeActionRows.filter(
    (row) => row.kind === 'action' && row.label.toLowerCase().includes(needle),
  );
  return {
    groups: rows.length === 0 ? [] : [{ id: 'actions', title: 'Actions', rows }],
    status: 'ready',
    note: null,
    retry: null,
  };
}
