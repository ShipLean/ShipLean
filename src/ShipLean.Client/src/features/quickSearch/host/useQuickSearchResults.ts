import { useActionSource } from '../start/useActionSource';
import { useRecentSource } from '../start/useRecentSource';
import { useSearchSource } from '../search/useSearchSource';
import type { QuickSearchResults } from '../types';
import { composeResults } from './composeResults';

// Calls all three sources on every render (they are hooks), so it is used only by the component that mounts while the
// dialog is open.
export function useQuickSearchResults(query: string): QuickSearchResults {
  const search = useSearchSource(query);
  const recent = useRecentSource();
  const actions = useActionSource(query);
  return composeResults(query, search, recent, actions);
}
